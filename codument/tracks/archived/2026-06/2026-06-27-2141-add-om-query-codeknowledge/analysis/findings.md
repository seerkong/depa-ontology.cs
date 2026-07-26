# Findings

## Found Facts
- `Om.Core` now exposes `CozoOm.Runtime.Store` through `ICozoOmStore`, making it a good execution boundary for query APIs.
- `Om.Batch` provides transactional ingestion patterns but is OM entity/property/edge oriented, while CodeKnowledge needs its own stored relations for code/doc facts.
- `Om.Analytics` already defines graph/tree/ranking result envelopes that can guide Om.Query result shapes.
- `Datalog.Core` and `Datalog.Cozo` exist as independent package projects under `cozo-lib-dotnet/packages/`.

## Constraints
- Do not move code/doc ontology into `Om.Core`.
- Do not implement MCP/server in this track.
- Do not allow unrestricted raw CozoScript as the primary agent entry.
- CodeKnowledge v1 should accept externally supplied facts rather than implementing a language parser or Git history crawler.

## Open Questions
- Whether `Om.Query` should later become an independent csproj.
- Whether wiki compiler should later write Markdown files or remain a plan-only API.

## Conclusions
- `Om.Query` should execute Portable Datalog through `Datalog.Core`/`Datalog.Cozo` and `ICozoOmStore`.
- `Om.CodeKnowledge` should provide stable CodeKnowledge relations and register default NamedQueries for agent facades.

## Implementation Evidence
- 2026-06-27T18:27:52Z：新增 `cozo-lib-dotnet/src/Om.Query/`，包含 NamedQuery models、registry、query engine、CozoOm extensions 和 README。
- 2026-06-27T18:27:52Z：新增 `cozo-lib-dotnet/src/Om.CodeKnowledge/`，包含 CodeKnowledge facts models、schema init、batch indexing、默认 NamedQuery registry、agent facade、wiki plan 和 README。
- 2026-06-27T18:27:52Z：`Cozo.DotNet.csproj` 已引用 `packages/Datalog.Core` 与 `packages/Datalog.Cozo`，供 `Om.Query` 编译 Portable Datalog。
- 2026-06-27T18:27:52Z：`cozo-lib-dotnet/tests/Program.cs` 已覆盖 NamedQuery happy path、invalid Portable Datalog diagnostics、CodeKnowledge indexing、symbol context、impact、docs、concept trace、relation explanation、wiki plan。
- 验证已通过：
  - `dotnet build cozo-lib-dotnet/Cozo.DotNet.csproj`
  - `dotnet run --project cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj`
- gap-loop 第 1 轮发现并修复 `code.impactOfChange` graph result-shape gap：NamedQuery 未投影 Source，且 graph mapping 对大小写敏感导致 graph edges 为空。
- gap-loop 第 2 轮返回 `NO_GAP`；graph result-shape fix 已通过 build、tests 和 codument validate 复检闭合。
