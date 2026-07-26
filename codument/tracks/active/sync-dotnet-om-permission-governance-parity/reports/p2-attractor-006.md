# P2 AttractorCheck 006

**Verdict: PASS**

## Scope

Fresh read-only final P2 audit of the current worktree for
`sync-dotnet-om-permission-governance-parity`. Read T2.1--T2.6,
the T2.7 root harness diagnosis, and P2.005, then re-audited D1=A's strict
witness, ABAC, single-normalized `AsOf`, empty-path, and field-hide rules.
No source, test, track, or mission state was changed by this check.

## Closure of P2.005

- **Strict witness and empty path: PASS.** .NET accepts a JSON `[]` path and
  witnesses it only when subject equals resource; directed traversal otherwise
  uses canonical outgoing relation hops and the boundary-normalized temporal
  point (`cozo-lib-dotnet/src/Om.Core/Logic/ConstraintLogic.cs:1605-1759`).
  Bun now retains exactly raw `[]` for the same zero-hop evaluator instead of
  discarding it (`cozo-lib-bun/cozo-om.js:1330-1357`). The Bun conformance case
  proves self allows and non-self denies.
- **Fail-closed ABAC: PASS.** Both sides restrict the reference shape,
  reject compatibility selectors as `malformed_reference`, and treat invalid
  operators/references or failed predicates as non-matches
  (`ConstraintLogic.cs:1762-1845`, `cozo-om.js:1119-1144,1360-1413`).
- **One-time temporal normalization: PASS.** `CheckAccessAsync` normalizes
  once at its request boundary and passes the value through witness and ABAC
  reads (`ConstraintLogic.cs:644-730,1743-1745,1840-1842`). Bun does the same
  with normalized-only neighbor/property helpers (`cozo-om.js:1074-1081,
  1252-1261,4039-4049,4561-4569`); its regression observes exactly one
  `Date.parse` call.
- **Field hide gating: PASS.** .NET adds hidden fields only after policy
  status is `Matched` and only for an allow effect (`ConstraintLogic.cs:731-769`).
  Bun now accumulates hide candidates locally and projects them only after the
  complete policy matches with allow effect (`cozo-om.js:1360-1422`). Its
  regression covers unwitnessed, deny, failed-ABAC, and allowed policies.

## Executed Evidence

- PASS: `/Users/kongweixian/.bun/bin/bun test __tests__/om-perm-governance-conformance.test.js`
  from `cozo-lib-bun`: 13 passed, 0 failed, 48 expectations. This includes the
  empty-self/non-self, temporal witness/ABAC, parse-once, conditional hide,
  invalid reference, wildcard, and deterministic-order cases.
- PASS: `MSBUILDTERMINALLOGGER=off dotnet build -tl:off tests/Cozo.DotNet.Om.Tests.csproj --no-restore`
  from `cozo-lib-dotnet`: 0 warnings, 0 errors.
- PASS: `COZO_OM_TEST_DIAGNOSTICS=1 COZO_OM_TEST_FOCUS=permission-governance tests/bin/Debug/net10.0/Cozo.DotNet.Om.Tests`
  from `cozo-lib-dotnet`: focused fixture passed, including invalid-policy,
  witness/empty-path, field-hide, and cancellation boundaries.
- PASS: `COZO_OM_TEST_DIAGNOSTICS=1 tests/bin/Debug/net10.0/Cozo.DotNet.Om.Tests`
  from `cozo-lib-dotnet`: full executable harness passed in 204.4 seconds.
  The root diagnosis's cwd requirement was honored; no external watchdog or
  permission-evaluator failure occurred.
- PASS: `codument validate sync-dotnet-om-permission-governance-parity --strict`:
  track XML and its behavior delta validate.

## Residual Boundary

This is a P2 implementation-attractor verdict, not completion of the pending
P3 cross-implementation verification/gap-loop tasks. Within the requested P2
surface, P2.005's two observable Bun/.NET parity gaps are closed and no new
strict-permission governance gap was found.
