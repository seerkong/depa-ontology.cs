# P2 Attractor Recheck 002: T2.4 Permission Governance

## Status: GAP

## Scope

Fresh, read-only recheck after T2.4 of the active C# evaluator against this track's D0/D1 decisions and the Bun reference. The audit covers wildcard policy matching, temporal `AsOf` normalization/projection, and strict witness/ABAC behavior. No production code, tests, or track state were changed.

## Passing Evidence

- **No action/resource wildcard grant:** C# evaluates a policy only when `policy.Action` equals the requested action and `policy.ResourceType` is in the exact resource/ancestor scopes; `*` enters neither condition. The new C# fixture proves wildcard allow and deny policies cannot grant access or override an explicit allow. Bun uses the same exact action check and its focused wildcard regression passes.
- **Observable normalized `AsOf`:** `CheckAccessAsync` normalizes `input.AsOf` before evaluation and projects that normalized value to both `CheckAccessResult.AsOf` and JSON `Explanation.asOf`, including fail-closed missing-entity results. The temporal fixture asserts `2024-06-01T00:00:00.000Z` in both projections.
- **Strict witness and policy result:** path rules are parsed/canonicalized, unknown or malformed paths are invalid, witness traversal is directed/outgoing and uses the supplied temporal point, and a policy contributes only when both witness and ABAC status are matched. Empty paths witness only subject == resource; deny remains dominant and hide effects require a matching policy.

## Findings

### P1: ABAC allows disallowed compatibility references

**GAP.** The track decision limits ABAC references to `subject.type`, `resource.type`, `subject.<attribute>`, `resource.<attribute>`, explicit literals, and the dedicated `field.<name> hide ...` shape. The implementation instead accepts `subject.id`, `action`, `resource.id`, and `resource.field` as valid references, then resolves them from request input. A policy using one of these values can therefore become a matched allow or deny instead of failing closed as an unsupported reference.

- Contract: `codument/tracks/sync-dotnet-om-permission-governance-parity/decisions.md:12`
- Acceptance path: `cozo-lib-dotnet/src/Om.Core/Logic/ConstraintLogic.cs:1771-1810`
- Request-value resolution: `cozo-lib-dotnet/src/Om.Core/Logic/ConstraintLogic.cs:1823-1831`
- Existing tests cover malformed `subject.` and unsupported operators, but not these four prohibited references: `cozo-lib-dotnet/tests/PermissionGovernanceParityFixtures.cs:64-77`

This also exceeds the Bun reference's supported non-literal reference surface, so it conflicts with D0 observable parity as well as D1's hard-cut rule.

### P2: `AsOf` is value-stable but not parsed exactly once

**GAP.** The access boundary normalizes `AsOf`, but the witness and ABAC leaf APIs normalize the already-normalized timestamp again for every temporal neighbor/property read. This preserves the currently asserted serialized value, but does not satisfy T2.4's stated "parse once" requirement and leaves future normalization behavior duplicated across authorization reads.

- Entry normalization: `cozo-lib-dotnet/src/Om.Core/Logic/ConstraintLogic.cs:649-651`
- Re-normalization for witness reads: `cozo-lib-dotnet/src/Om.Core/Logic/RelationLogic.cs:124-134`
- Re-normalization for ABAC reads: `cozo-lib-dotnet/src/Om.Core/Logic/EntityLogic.cs:178-187`

The temporal fixture proves consistent output, not single parsing: `cozo-lib-dotnet/tests/PermissionGovernanceParityFixtures.cs:224-230`.

## Verification

```sh
cd cozo-lib-bun
bun test __tests__/om-perm-governance-conformance.test.js
```

Result: 9 passed, 0 failed, 25 expectations.

```sh
cd cozo-lib-dotnet
dotnet run --project tests/Cozo.DotNet.Om.Tests.csproj
```

Result: complete executable harness exited successfully.

## Closure Conditions

1. Reject the four compatibility references as `malformed_reference` (or amend the track decision through an explicit approved compatibility decision), with C# and Bun parity regressions proving they cannot grant or deny.
2. Pass a normalized temporal value through internal authorization read helpers without reparsing it, then add an internal-focused assertion or a suitable seam proving the one-normalization boundary.

