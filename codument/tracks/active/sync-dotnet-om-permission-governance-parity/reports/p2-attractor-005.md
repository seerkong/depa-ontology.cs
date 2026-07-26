# P2 AttractorCheck 005

**Verdict: GAP**

## Scope

Read-only final coding audit of P2 T2.1--T2.6 against D1=A: strict, fail-closed
permission evaluation with observable Bun/.NET parity, including one-time Bun `AsOf`
normalization. No source or track-state files were changed.

## Finding

1. **P1 parity gap: `[]` has different zero-hop witness semantics.**
   .NET accepts a JSON empty path and treats it as a witness only when subject and
   resource are identical ([ConstraintLogic.cs](../../../../cozo-lib-dotnet/src/Om.Core/Logic/ConstraintLogic.cs#L1679-L1729)); its fixture requires that
   `empty-self` allows and `empty-other` denies
   ([PermissionGovernanceParityFixtures.cs](../../../../cozo-lib-dotnet/tests/PermissionGovernanceParityFixtures.cs#L189-L200)).
   Bun's evaluator has the same zero-hop helper branch, but discards every parsed
   empty path before calling it ([cozo-om.js](../../../../cozo-lib-bun/cozo-om.js#L1330-L1355)). Thus a Bun policy with path `[]` always denies,
   including subject == resource. This is fail-closed, but it is not D1 observable
   parity and contradicts the track decision that an empty path witnesses only self.

2. **P1 field-result gap: Bun projects hide effects from an unmatched policy.**
   Bun mutates the result-wide `fieldVisibility` as soon as a `hide` expression is
   truthy, before evaluating later non-hide predicates
   ([cozo-om.js](../../../../cozo-lib-bun/cozo-om.js#L1360-L1412)). A no-write probe with
   `field.secret hide true` followed by `subject.role == admin` for a `viewer`
   subject returned `{"allow":false,"fieldVisibility":{"secret":"hidden"},"matched":false}`.
   .NET delays the field projection until a policy has `Matched` status. This
   contradicts D1's field-hide rule and causes observable result drift.

## T2 Assessment

| Task | Result | Evidence |
| --- | --- | --- |
| T2.1 strict witnesses | GAP | Directed/current-or-`AsOf` traversal and deterministic ordering are present; the empty self-witness divergence above remains. |
| T2.2 entity-aware ABAC / fields | GAP | Reference validation and deny precedence are fail-closed, but Bun leaks a truthy hide result from an otherwise unmatched policy. |
| T2.3 hard cut / facade | PASS | .NET's legacy `subject->project` fixture is rejected with `unknown_relation`; no explanatory permissive fallback was found. |
| T2.4 wildcard / projected `AsOf` | PASS | Exact action/resource matching excludes wildcard policies; .NET fixture verifies normalized `AsOf` in typed and JSON results. |
| T2.5 strict references / .NET normalization | PASS | `CheckAccessAsync` normalizes at its boundary and passes the value to normalized witness and property helpers. |
| T2.6 Bun normalization | PASS | Bun normalizes at `checkAccess` entry and reuses the value in temporal property and neighbor helpers; its direct parse-count conformance case passes. |

## Test Evidence

- PASS: `cd /Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-bun && /Users/kongweixian/.bun/bin/bun test __tests__/om-perm-governance-conformance.test.js`
  - 11 passed, 0 failed. This includes temporal graph/ABAC behavior and the
    single-normalization `AsOf` probe.
- PASS: `codument validate sync-dotnet-om-permission-governance-parity --strict`
  - Track XML and its behavior delta validate.
- REPRODUCED GAP: an inline Bun probe seeded `field.secret hide true` before a
  failing `subject.role == admin` predicate.
  - Output: `{"allow":false,"fieldVisibility":{"secret":"hidden"},"matched":false}`.
- PASS: `cd /Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet && COZO_OM_TEST_DIAGNOSTICS=1 dotnet run --no-build --project tests/Cozo.DotNet.Om.Tests.csproj -c Release`
  - Completed in 201.6 seconds with `Cozo.DotNet OM tests passed.`

## Required Closure

Align Bun's parsed-path loop with the agreed `[]` self-witness rule and add the
same Bun conformance case. Defer field-visibility projection until the complete
policy has matched, then add the conditional-hide regression case. Re-run the
targeted Bun suite and complete the .NET harness before returning P2 to PASS.
