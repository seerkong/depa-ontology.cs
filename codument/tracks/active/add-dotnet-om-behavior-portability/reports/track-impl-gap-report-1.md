# Gap Loop Report Round 1

Track: add-dotnet-om-behavior-portability
Scope: phase P4
Status: FIX_APPLIED

## Inputs Read

- `proposal.md`
- `design.md`
- `behavior_deltas/cozo-dotnet-om/delta.xml`
- `track.xml`
- `reports/` history: no previous gap reports found
- Current implementation under `cozo-lib-dotnet/src/Om.Core/`, `cozo-lib-dotnet/packages/Om.Portability.Yaml/`, and `cozo-lib-dotnet/tests/`
- Current uncommitted diff and status. Existing unrelated `cozo-lib-dotnet-llm-wiki/` changes were not modified.

## Target Comparison

P4 requires final validation plus two late hardening tasks:

- Staged registry publication must use expected-snapshot CAS and preserve a concurrent registry writer.
- Canonical manifest decode/import must reject cross-kind metadata/key combinations before effects.
- The broader track requires honest restart/readiness projection, fail-closed behavior entry points, atomic import/compensation, YAML safety/dependency isolation, and no expansion into Jint/G9 validation facade scope.

Static review found the implementation broadly covers the requested surfaces:

- `BehaviorImportLogic` stages in memory, preflights requireReady, commits persistence, publishes by expected registry snapshot, and compensates persistence on publish failure.
- `BehaviorManifestJsonCodec` validates version, kind, callback slots, duplicate keys, interceptor keys, and cross-kind metadata.
- `BehaviorManifestYamlAdapter` stays outside Om.Core, pins YamlDotNet 18.1.0, parses a bounded event AST, and rejects tags, anchors, aliases, merge keys, duplicate keys, multiple documents, depth, node, and byte limit violations.
- Runtime readiness checks are present across constraint, computed, action, parent action, mutation, and interceptor resolution.

## Gap Found

Native interceptor allocation after an unresolved imported sparse interceptor only considered the executable registry, not persisted interceptor metadata.

The design and behavior delta require exact sparse interceptor keys to be preserved and future local sequence allocation to use max(seq)+1. The ready-path test covered imported sparse metadata because a ready import also stages a registry entry. However, an unresolved import persists metadata and binding rows without an executable registry entry. In that case `AddInterceptorCallbackAsync` called `runtime.Registry.NextInterceptorSeq(...)`, which could allocate a lower sequence and ignore the unresolved imported metadata key.

Impact: after importing unresolved interceptor metadata at `before/21`, a native `AddInterceptorAsync(..., "before", handler)` could allocate from the registry-only max instead of metadata max, violating exact-key portability and sparse sequence semantics.

## Fix Applied

- Updated `ConstraintLogic.AddInterceptorCallbackAsync` to allocate interceptor sequence from the maximum of:
  - persisted `om_interceptor_def` sequences for the owner/action/phase, and
  - current executable registry sequences.
- Added a regression test that imports unresolved sparse interceptor metadata at seq 21, then verifies native allocation uses seq 22 while seq 21 remains unresolved and without a registry callback.

## Verification

Passed:

- `git diff --check -- cozo-lib-dotnet/src/Om.Core/Logic/ConstraintLogic.cs cozo-lib-dotnet/tests/Program.cs codument/tracks/add-dotnet-om-behavior-portability`

Blocked in this environment:

- `dotnet` is not installed on PATH, so the .NET OM build/tests could not be executed.
- `codument` is not installed on PATH, so structural Codument validation could not be executed.

## Residual Risk

The fix is localized and covered by a direct regression test, but runtime verification remains pending until a .NET SDK is available.
