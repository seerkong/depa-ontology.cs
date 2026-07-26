# P2 AttractorCheck 001: Strict Permission Governance

## Status: GAP

## Scope and Evidence

Audited the active track contract, P1/P2 reports, current C# diff, Bun reference, and `codument/attractors/project.md`. Re-ran:

```sh
cd cozo-lib-dotnet/tests
dotnet build Cozo.DotNet.Om.Tests.csproj --no-restore
COZO_OM_TEST_DIAGNOSTICS=1 dotnet run --no-build --project Cozo.DotNet.Om.Tests.csproj

cd cozo-lib-bun
bun test __tests__/om-perm-check-access.test.js __tests__/om-perm-governance-conformance.test.js
```

The .NET build and full harness passed with zero warnings/errors. The Bun focused suites passed 11 tests / 33 expectations.

## Findings

### P1: C# wildcard policy matching is a Bun-incompatible grant surface

`CheckAccessAsync` treats `policy.Action == "*"` as matching every requested action and unconditionally includes `"*"` in resource scopes. A witnessed allow policy with either C# wildcard therefore grants access for actions/resources for which Bun would not match that policy. The Bun reference requires exact action equality and only applies a resource type policy after resolving that declared type; it has no matching wildcard branch.

- C#: `cozo-lib-dotnet/src/Om.Core/Logic/ConstraintLogic.cs:672-716`
- Bun: `cozo-lib-bun/cozo-om.js:1281-1297`
- Contract: D0 observable parity and D1 strict hard cut in `decisions.md`; no intentional wildcard divergence is recorded.

This is fail-open relative to the selected Bun contract, even though the path witness and ABAC predicates still have to match. Reject or give a documented, explicitly approved interpretation to `*` at the public policy-definition boundary, and add two-runtime tests for action and resource wildcard inputs.

### P2: Detached explanations omit the normalized temporal decision point

The C# evaluator normalizes and applies `AsOf` to witness/property reads, but the serialized `Explanation` does not include that normalized value. A persisted explanation therefore cannot establish which temporal graph/property state produced its decision. Bun includes normalized `asOf` in its explanation input.

- C# normalization/use: `cozo-lib-dotnet/src/Om.Core/Logic/ConstraintLogic.cs:649-651`, `719-733`
- C# explanation projection: `cozo-lib-dotnet/src/Om.Core/Logic/ConstraintLogic.cs:773-789`
- Bun explanation input: `cozo-lib-bun/cozo-om.js:1120-1136`

Include the normalized `AsOf` in the serializable explanation for both normal and fail-closed results, then assert it in the temporal fixture.

## Checks That Passed

- Strict witnesses only contribute after an ordered, outgoing, current/AsOf traversal; empty paths only witness self; invalid paths fail closed.
- All evaluated store and helper reads receive the caller cancellation token. `OperationCanceledException` is not caught as a missing entity/property, and the typed facade has cancellation coverage.
- Policy, ABAC rule, path, witness-hop, diagnostic, and hidden-field collection order is explicitly canonicalized before explanation projection.
- Deny remains final precedence over matching allow policies; matched hide effects do not change the final allow calculation.

## Residual Test Need

The passing matrix does not exercise either wildcard policy form or assert normalized `AsOf` in the C# explanation. Those cases are required to close the two findings above.
