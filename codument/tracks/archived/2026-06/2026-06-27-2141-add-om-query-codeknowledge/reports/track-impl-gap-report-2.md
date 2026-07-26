# Gap Report Round 2: add-om-query-codeknowledge

## Scope

- Track: `add-om-query-codeknowledge`
- Scope: `track`
- Round: `2`
- Mode: FIX follow-up lightweight re-check
- Role: fresh gap-loop round executor

## Inputs Read

- `codument/std/operations/gap-loop.md`
- `codument/tracks/add-om-query-codeknowledge/reports/track-impl-gap-report-1.md`
- `codument/tracks/add-om-query-codeknowledge/track.xml`
- `codument/tracks/add-om-query-codeknowledge/proposal.md`
- `codument/tracks/add-om-query-codeknowledge/design.md`
- `codument/tracks/add-om-query-codeknowledge/behavior_deltas/dotnet-code-knowledge/delta.xml`
- `codument/tracks/add-om-query-codeknowledge/behavior_deltas/dotnet-om-query/delta.xml`
- `cozo-lib-dotnet/src/Om.CodeKnowledge/CozoOmCodeKnowledgeExtensions.cs`
- `cozo-lib-dotnet/src/Om.Query/OmQueryEngine.cs`
- `cozo-lib-dotnet/tests/Program.cs`

## Focus

This round re-checked only the previous round's graph result-shape fix:

- whether `code.impactOfChange` projects graph edge columns as `Source`, `Target`, and `Kind`;
- whether `OmQueryEngine` graph mapping accepts `Source` / `Target` / `Kind` and lowercase variants;
- whether tests cover a non-empty graph edge result for the NamedQuery.

## Findings

No remaining gap was found in the previous fix scope.

- `code.impactOfChange` now uses `?- impact(Source, Target, Kind), Source = $symbolId.`, so the graph-shaped NamedQuery returns explicit source, target, and edge kind columns.
- `OmQueryEngine.ToGraph` still probes graph fields using lowercase canonical names, but `StringValue` compares row keys with `StringComparison.OrdinalIgnoreCase`. This supports `Source` / `Target` / `Kind`, `source` / `target` / `kind`, and the existing aliases such as `from_id` / `to_id`.
- `cozo-lib-dotnet/tests/Program.cs` executes `code.impactOfChange` through `ExecuteNamedAsync` and asserts `impactGraph.Graph is { Edges.Count: > 0 }`, covering the non-empty graph edge result shape.

## Verification

All required verification commands passed:

- `dotnet build cozo-lib-dotnet/Cozo.DotNet.csproj`
  - Result: passed, 0 warnings, 0 errors.
- `dotnet run --project cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj`
  - Result: passed, output `Cozo.DotNet OM tests passed.`
- `codument validate add-om-query-codeknowledge --strict`
  - Result: passed, `track.xml OK + 2 behavior delta(s)`.

## Verdict

Status: `NO_GAP`

The previous graph result-shape gap is closed. No implementation, behavior delta, design, or track changes were needed in this round.
