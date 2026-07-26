# Gap Loop Report Round 2

Track: add-dotnet-om-behavior-portability
Scope: phase P4
Status: NO_GAP
Mode: light fix recheck after Round 1 FIX_APPLIED

## Inputs Read

- `codument/std/operations/gap-loop.md`
- `proposal.md`
- `design.md`
- `behavior_deltas/cozo-dotnet-om/delta.xml`
- `track.xml`
- `reports/track-impl-gap-report-1.md`
- Current implementation diff around `ConstraintLogic.NextInterceptorSeqAsync` and `ConstraintLogic.AddInterceptorCallbackAsync`
- Current regression in `cozo-lib-dotnet/tests/Program.cs` around unresolved sparse interceptor import

## Recheck Focus

Round 1 found that native interceptor allocation after an unresolved sparse import used registry-only sequencing. The required closure was:

- permissive import persists unresolved interceptor metadata and binding identity without publishing a registry callback;
- later native interceptor allocation for the exact owner/action/phase uses `max(persistent metadata seq, registry seq) + 1`;
- the imported unresolved key remains preserved as unresolved and is not overwritten by the native registration.

## Findings

No remaining gap found in the narrow Round 2 surface.

`ConstraintLogic.AddInterceptorCallbackAsync` resolves the exact owner/action/phase and delegates sequence allocation to `NextInterceptorSeqAsync`. `NextInterceptorSeqAsync` reads existing `om_interceptor_def` rows for that exact owner/action/phase, computes `metadata max + 1`, compares it with `runtime.Registry.NextInterceptorSeq(...)`, and returns the maximum. This closes the Round 1 gap because unresolved imports are represented in persistent metadata even when no registry callback exists.

The new regression is executable and meaningful. It imports an interceptor catalog entry at `ImportOwner/portable_action/before/21` without a callback binding set, verifies the import is applied with one unresolved diagnostic, adds a native interceptor, then asserts:

- the imported seq `21` still has binding id `import:interceptor:21` and readiness `Unresolved`;
- no registry callback exists at seq `21`;
- the native interceptor is allocated at seq `22`;
- seq `22` is unbound and has the native description.

The test therefore exercises the exact missing condition from Round 1: persistent unresolved metadata must influence future native sequence allocation even when registry state is sparse.

## Verification

Passed:

- `git diff --check -- cozo-lib-dotnet/src/Om.Core/Logic/ConstraintLogic.cs cozo-lib-dotnet/tests/Program.cs codument/tracks/add-dotnet-om-behavior-portability`
- `cd cozo-lib-dotnet && /usr/local/share/dotnet/dotnet run --project tests/Cozo.DotNet.Om.Tests.csproj`

Result:

- `Cozo.DotNet OM tests passed.`

## Residual Risk

No obvious new gap was found in the narrow surface. This round did not perform a broad re-review outside `ConstraintLogic.NextInterceptorSeqAsync` / `AddInterceptorCallbackAsync` and the targeted unresolved sparse interceptor regression.
