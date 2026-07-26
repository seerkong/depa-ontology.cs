# Knowledge Context

## Source Notes
| Source | Summary | Relevance |
|--------|---------|-----------|
| `cozo-lib-dotnet/src/Om.Core/**` | Core ontology, store abstraction, transactions, schema/type/entity/relation/constraint/action/existential/permission. | Base fact source and execution boundary. |
| `cozo-lib-dotnet/src/Om.Batch/**` | Batch ingestion extension with transaction and validation pattern. | Pattern for CodeKnowledge indexing. |
| `cozo-lib-dotnet/src/Om.Analytics/**` | Template result envelope and graph/tree/ranking visual models. | Pattern for query result shapes. |
| `cozo-lib-dotnet/packages/Datalog.*` | Portable Datalog parser/validator/IR and CozoScript backend compiler. | Language and compiler substrate for Om.Query. |

## Codebase Knowledge
- Main `Cozo.DotNet.csproj` compiles `src/**/*.cs` and can reference Datalog package projects.
- Tests are currently console-style assertions in `cozo-lib-dotnet/tests/Program.cs`.

## Domain Knowledge
- NamedQuery is the safe default for agents; ad-hoc Portable Datalog is the advanced path.
- CodeKnowledge facts should be first-class Cozo stored relations because they are not ordinary business entities.
- Wiki plan output should be structured enough for future MCP/wiki compiler layers.

## Terms
| Term | Meaning |
|------|---------|
| Om.Query | OM query product layer over Portable Datalog and CozoScript execution. |
| Om.CodeKnowledge | Code/document knowledge graph domain capsule. |
| NamedQuery | Stable query definition with parameters, relation mappings, result shape, and safety policy. |
| Wiki plan | Structured plan describing wiki pages, source files, symbols, docs, missing docs, and stale docs. |
