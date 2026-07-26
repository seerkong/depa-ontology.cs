# Gap Report Round 1: add-om-query-codeknowledge

## Scope

- Track: `add-om-query-codeknowledge`
- Scope: `track`
- Round: `1`
- Role: fresh gap-loop round executor

## Inputs Read

- `codument/tracks/add-om-query-codeknowledge/proposal.md`
- `codument/tracks/add-om-query-codeknowledge/design.md`
- `codument/tracks/add-om-query-codeknowledge/behavior_deltas/dotnet-code-knowledge/delta.xml`
- `codument/tracks/add-om-query-codeknowledge/behavior_deltas/dotnet-om-query/delta.xml`
- `codument/tracks/add-om-query-codeknowledge/track.xml`
- `codument/tracks/add-om-query-codeknowledge/analysis/findings.md`
- Current implementation under `cozo-lib-dotnet/src/Om.Query/**`
- Current implementation under `cozo-lib-dotnet/src/Om.CodeKnowledge/**`
- `cozo-lib-dotnet/Cozo.DotNet.csproj`
- Om.Query / Om.CodeKnowledge tests in `cozo-lib-dotnet/tests/Program.cs`

No prior gap reports existed under `codument/tracks/add-om-query-codeknowledge/reports/` before this round.

## Target Comparison

The implementation matches the main track goals:

- `Om.Query` provides NamedQuery definitions, parameter schema, registry, Portable Datalog execution through `Datalog.Core` and `Datalog.Cozo`, immutable store execution by default, diagnostics for parse/validation/compile failures, and table/graph/raw result envelopes.
- `Om.CodeKnowledge` provides independent `ck_*` stored relations, schema initialization, fact batch indexing, default agent-facing NamedQueries, facade APIs for symbol context, impact, docs, concept trace, relation explanation, and a plan-only wiki result.
- `Cozo.DotNet.csproj` references the Datalog package projects.
- Tests cover NamedQuery happy path, invalid Portable Datalog diagnostics, CodeKnowledge indexing, facades, and wiki plan.

## Gap Found

One implementation gap was found in graph result mapping for the default impact query:

- `code.impactOfChange` declared `OmQueryResultShape.Graph` but its query projected `Target` and `Kind` without a source column.
- `OmQueryEngine.ToGraph` only looked for lowercase `source` / `target` / `kind` style column names using case-sensitive dictionary lookup.
- Result: the NamedQuery execution could succeed while `OmQueryExecutionResult.Graph.Edges` was empty, so the `graph result shape` acceptance criterion was only partially satisfied.

## Fix Applied

Applied a small implementation fix:

- Updated the default `code.impactOfChange` NamedQuery to project `Source`, `Target`, and `Kind`.
- Updated `OmQueryEngine` graph mapping to resolve graph columns case-insensitively, so `Source` / `Target` / `Kind` and lowercase variants both work.
- Added a test assertion that `code.impactOfChange` produces a non-empty graph edge set through `ExecuteNamedAsync`.

Changed files:

- `cozo-lib-dotnet/src/Om.CodeKnowledge/CozoOmCodeKnowledgeExtensions.cs`
- `cozo-lib-dotnet/src/Om.Query/OmQueryEngine.cs`
- `cozo-lib-dotnet/tests/Program.cs`

## Verification

All required verification commands passed after the fix:

- `dotnet build cozo-lib-dotnet/Cozo.DotNet.csproj`
  - Result: passed, 0 warnings, 0 errors.
- `dotnet run --project cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj`
  - Result: passed, output `Cozo.DotNet OM tests passed.`
- `codument validate add-om-query-codeknowledge --strict`
  - Result: passed, `track.xml OK + 2 behavior delta(s)`.

Additional check:

- `git diff --check -- cozo-lib-dotnet/src/Om.Query/OmQueryEngine.cs cozo-lib-dotnet/src/Om.CodeKnowledge/CozoOmCodeKnowledgeExtensions.cs cozo-lib-dotnet/tests/Program.cs`
  - Result: passed.

## Verdict

Status: `FIX_APPLIED`

The round found a small graph result-shape gap and repaired it. A follow-up fresh round should re-check the fix per gap-loop protocol.
