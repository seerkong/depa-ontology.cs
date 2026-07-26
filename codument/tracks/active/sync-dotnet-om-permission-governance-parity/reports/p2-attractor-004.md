# P2 Coding Attractor 004

## Verdict

GAP

## Scope

Fresh post-T2.5 corrective recheck of strict permission governance against D1=A and T2.5. This audit covers the ABAC reference whitelist and the requirement that one authorization request normalizes `AsOf` once at its boundary and reuses that normalized value for all temporal witness and ABAC reads.

## Finding

### P0: Bun authorization still reparses normalized `AsOf`

`checkAccess` normalizes `payload.asOf` at its boundary, but its temporal witness helper calls public `getNeighborsAsOf`, which calls `_normalizeAsOfTimestamp` again. Its temporal ABAC property helper likewise calls public `getPropertyAsOf`, which reparses the same value. A multi-hop witness or multiple ABAC property predicates therefore performs multiple internal timestamp normalizations.

- Boundary normalization: `cozo-lib-bun/cozo-om.js:1147-1155`.
- Witness path: `_getOutgoingNeighborsForPerm` calls `getNeighborsAsOf` at `cozo-lib-bun/cozo-om.js:1075-1081`; that public helper normalizes at `cozo-lib-bun/cozo-om.js:4549-4553`.
- ABAC path: `getProp` calls `getPropertyAsOf` at `cozo-lib-bun/cozo-om.js:1252-1261`; that public helper normalizes at `cozo-lib-bun/cozo-om.js:4032-4038`.

This fails T2.5-AC2. The C# side is corrected: `CheckAccessAsync` normalizes at `ConstraintLogic.cs:644-651` and reuses it through normalized-only relation/property helpers at `ConstraintLogic.cs:1735-1743` and `ConstraintLogic.cs:1830-1840`. The Bun reference must provide equivalent internal normalized temporal-read helpers and use them from permission evaluation before T2.5 can pass.

## Confirmed Passes

- The decision whitelist rejects `subject.id`, `action`, `resource.id`, and `resource.field`; malformed compatibility references fail closed in C# typed and JSON diagnostics (`ConstraintLogic.cs:1759-1812`, `PermissionGovernanceParityFixtures.cs:272-283`) and in Bun (`cozo-om.js:1119-1144`, `om-perm-governance-conformance.test.js:217-243`).
- `MSBUILDTERMINALLOGGER=off dotnet run --project tests/Cozo.DotNet.Om.Tests.csproj` in `cozo-lib-dotnet` passed: `Cozo.DotNet OM tests passed.`
- `/Users/kongweixian/.bun/bin/bun test __tests__/om-perm-governance-conformance.test.js` in `cozo-lib-bun` passed: 10 tests, 0 failures.

## Required Correction

Add Bun internal temporal neighbor/property readers that accept an already-normalized `AsOf`, route permission witness and ABAC resolution through them, and add a focused regression that proves authorization evaluation does not call timestamp normalization after the boundary.
