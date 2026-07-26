# Findings

## Found Facts
- `cozo-lib-dotnet` is currently a thin .NET 10 binding over `cozo_c`; public operations are concentrated in `src/CozoDb.cs` and native interop in `src/CozoNative.cs`.
- `cozo-lib-dotnet/Cozo.DotNet.csproj` includes `src/**/*.cs`, so files placed under `cozo-lib-dotnet/src/Om/` are compiled and packed without additional project-file changes.
- The existing Node/Bun ontology layer lives in `cozo-lib-bun/cozo-om.js` with public type declarations in `cozo-lib-bun/cozo-om.d.ts`.
- The Node OM feature surface includes schema initialization, type hierarchy, attributes, relations, entities, properties, temporal reads, constraints, computed values, actions/mutations/interceptors, schema versioning, permissions, graph utilities, and existential rules.
- Existential rules are already scoped in Node OM v1 to `exists: { rel, direction?, toType }`; attribute existence remains covered by required attributes and validation.

## Constraints
- The C# implementation SHALL live inside `cozo-lib-dotnet/src/Om/` as part of the existing `cozo-lib-dotnet` package.
- The implementation SHOULD follow DEPA: `output = fn(runtime, input, config)`, explicit runtime dependencies, contract/logic/support separation, and runtime as a data carrier.
- `CozoDb.Run` and other direct Cozo effects MUST be isolated behind a store/effect contract; core OM logic MUST NOT directly call native bindings.
- Public API may be ergonomic, but it must remain a thin facade over DEPA-style logic functions.
- C# v1 parity should track Node OM semantics before extending behavior.

## Open Questions
- Exact test framework choice for `cozo-lib-dotnet` is not yet present in the package. The implementation phase should add a minimal compatible test project or use the repo-preferred .NET test setup after inspection.
- The first implementation slice may choose between broad API scaffolding with partial implementation or narrow end-to-end functionality. The recommended track plan below uses narrow end-to-end slices.

## Conclusions
- Use Option A: implement the C# OM under `cozo-lib-dotnet/src/Om/`.
- Do not port `cozo-om.js` line-by-line. Build a DEPA capsule with `Contracts/`, `Runtime/`, `Inputs/`, `Logic/`, `Support/`, and `Internals/`.
- Store-backed relations in Cozo are authoritative facts; schema snapshots, diffs, and derived views are projections/checkpoints and must not become write sources.

## Implementation Findings

- Per user instruction, `codument-impl-track` and the subsequent gap-loop were executed in the main agent context instead of spawning subagents.
- Implemented `cozo-lib-dotnet/src/Om/` as a DEPA capsule with `CozoOm` facade, `ICozoOmStore`, runtime/options/registry, input records, logic modules, support store, and internals.
- `Logic/` uses `ICozoOmStore` and does not directly reference `CozoDb` or `CozoNative`; the concrete database effect is isolated in `Support/CozoDbOmStore.cs`.
- Added a console assertion test project at `cozo-lib-dotnet/tests/` because the existing .NET package had no test framework baseline. It runs with `dotnet run --project cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj`.
- The C# OM v1 covers schema initialization, type/attribute/relation definition, aliases, entity/property/edge operations, type inheritance queries, temporal property/edge reads, required/custom validation, metadata-backed computed/actions/mutations/interceptors, schema snapshot/diff/rollback, permission allow/deny/field/path checks, and existential rules.
- Existential rules intentionally keep the agreed v1 head scope: `exists { rel, direction?, toType }`; attribute existence remains represented by required attributes plus validation.

## Verification Evidence

- `MSBUILDTERMINALLOGGER=off dotnet build cozo-lib-dotnet/Cozo.DotNet.csproj` passed.
- `MSBUILDTERMINALLOGGER=off dotnet run --project cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj` passed.
- `codument validate add-dotnet-om-depa --strict` passed.
- `rg -n "CozoDb|CozoNative" cozo-lib-dotnet/src/Om/Logic cozo-lib-dotnet/src/Om/Runtime cozo-lib-dotnet/src/Om/Inputs cozo-lib-dotnet/src/Om/Contracts cozo-lib-dotnet/src/Om/Internals || true` produced no matches.
