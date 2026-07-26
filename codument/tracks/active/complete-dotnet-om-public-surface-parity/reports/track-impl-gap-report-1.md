# G9 P2 GapLoop Round 1

## Decision

**NO_GAP** within the G9 P2 phase scope. No production source or test change
was required.

## Scope and Attribution

This fresh review covered only the G9 files declared by
`analysis/scope-boundary.md`:

- `cozo-lib-dotnet/src/Om.Core/Contracts/Models/OmModels.cs`
- `cozo-lib-dotnet/src/Om.Core/Inputs/OmInputs.cs`
- `cozo-lib-dotnet/src/Om.Core/CozoOm.cs`
- `cozo-lib-dotnet/src/Om.Core/Logic/TypeLogic.cs`
- `cozo-lib-dotnet/src/Om.Core/Logic/ConstraintLogic.cs`
- `cozo-lib-dotnet/src/Om.Core/Logic/RelationLogic.cs`
- `cozo-lib-dotnet/src/Om.Core/Internals/OmConvert.cs`
- `cozo-lib-dotnet/tests/PublicSurfaceParityFixtures.cs`

The shared worktree contains concurrent Bun, ontology/wiki, schema, permission,
existential, action, portability, scripting, and other mission changes. Those
files, and unrelated hunks in otherwise attributable .NET files, were excluded
from the G9 decision and were not reverted or modified.

## Required Inputs Read

Read in this round:

- `proposal.md`
- `design.md`
- `behavior_deltas/public-surface/delta.xml`
- `track.xml`
- `analysis/scope-boundary.md`
- `reports/t2.1-refresh.md`
- `reports/t2.2-refresh.md`
- `reports/p1-attractor-003.md`
- `reports/p2-attractor-001.md`
- current G9 source, focused fixture, and attributable uncommitted diff hunks
- `codument/std/operations/gap-loop.md`, the repository's existing GapLoop SOP

The user-specified path `codument/std/sop/gap-loop.md` does not exist in this
checkout (`test -f` returned exit code 1). The available
`codument/std/operations/gap-loop.md` was read instead. No decision was blocked
by this repository-layout discrepancy.

## Target Comparison

### Typed parent updates and legacy compatibility

**PASS.** `TypeParentPatchKind` and `TypeParentPatch` expose distinct
`Keep`, `Set(parentType)`, and `Clear` intents. `Set` rejects null/blank parent
names, and non-`Set` patches cannot carry a parent. The typed
`CozoOm.DefineTypeAsync(DefineTypePatchInput, ...)` facade dispatches each
intent in `TypeLogic` without using null as a discriminant.

The existing nullable `DefineTypeAsync` overload preserves an existing parent
when `parentType` is null, matching the required legacy null=Keep migration
behavior. The focused fixture exercises legacy Keep and typed Clear, Set, and
Keep transitions.

### Public facades and minimal public surface

**PASS.** `CozoOm` exposes the requested public methods:

- `ValidateConstraintsAsync(string, IReadOnlyList<string>?, CancellationToken)`
- `TraverseAsync(string, IReadOnlyList<string>?, CancellationToken)`
- `InferValueType(object?)`
- `DefineTypeAsync(DefineTypePatchInput, CancellationToken)`

The facades delegate to existing core logic and do not require unrelated public
APIs for the G9 claim. Effectful methods retain cancellation tokens.

### Value-type inference and D0=A

**PASS.** `OmConvert.InferValueType` maps strings, booleans, numeric CLR
values, and JSON scalar `JsonElement` values consistently with the Bun
reference. CLR `null`, JSON `null`, and JSON `undefined` map to
`OmValueType.Unknown`; object/array-shaped values map to `Json`.

The implementation is at `OmConvert.cs:44-61`; the focused fixture covers the
public null mapping, and the JSON-null branch was verified by direct source
comparison with Bun's `inferValueType` contract (`cozo-om.js:121-135`).

`ValidateConstraintsAsync` preserves constraint-only validation and filtering,
while `TraverseAsync` returns the start entity for an empty path and follows
outgoing relation steps with deterministic de-duplication, matching the Bun
observable behavior.

### Registration guardrails and D5=C

**PASS.** Persistent behavior definitions resolve and validate the owner before
metadata writes. Constraint types are normalized and rejected unless supported
before the constraint metadata `:put`. Interceptor phases are normalized and
rejected unless `before` or `after` before metadata writes. Callback-bearing
registration paths perform these checks before registry updates and compensate
metadata/registry state if a later callback registration step fails.

The focused fixture rejects a missing owner, invalid constraint scope/type, and
invalid interceptor phase, then checks both persistent metadata and callback
registry state for absence of ghosts. The relevant ordering is visible in
`ConstraintLogic.cs:15-26`, `ConstraintLogic.cs:65-77`, and
`ConstraintLogic.cs:184-212`.

## Current Uncommitted Diff Review

The declared G9 source files are modified or, for the focused fixture, newly
untracked in the shared worktree. The G9-relevant implementation is present in
the expected symbols and passes the focused fixture. Large unrelated hunks in
`ConstraintLogic.cs`, and all changes outside the declared attribution list,
were excluded under the scope boundary. No G9 source regression or missing
required public symbol was found.

## Verification Evidence

Commands run in this round:

```text
MSBUILDTERMINALLOGGER=off /usr/local/share/dotnet/dotnet build tests/Cozo.DotNet.Om.Tests.csproj -v:q
```

Result: passed; 0 warnings, 0 errors.

```text
COZO_OM_TEST_FOCUS=public-surface MSBUILDTERMINALLOGGER=off /usr/local/share/dotnet/dotnet run --project tests/Cozo.DotNet.Om.Tests.csproj --no-build
```

Result: `Focused public surface parity fixture passed.`

```text
/Users/kongweixian/.bun/bin/bun test __tests__/om-type-hierarchy.test.js __tests__/om-constraint-conditional.test.js __tests__/om-constraint-cross-entity.test.js __tests__/om-constraint-inheritance.test.js __tests__/om-templates-and-batch.test.js __tests__/om-interceptor.test.js
```

Result: 36 passed, 0 failed, 97 expect calls across 6 files.

```text
git diff --check -- <declared G9 files>
```

Result: passed with no output.

## Changed Files

No source, Bun, test, track XML, behavior delta, or design files were changed
for this round. This report is the only round-1 artifact written.

## Final Status

There is no attributable G9 P2 gap. The required typed parent API, legacy
null=Keep behavior, public validation/traversal/inference facades, null/JSON
null `Unknown` inference, fail-before-effect guardrails, no-ghost behavior, Bun
observable parity, minimal public surface, D0=A, and D5=C checks are satisfied.
