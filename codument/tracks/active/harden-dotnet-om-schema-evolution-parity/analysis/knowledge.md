# Knowledge Context

## Source Notes

| Source | Summary | Relevance |
|---|---|---|
| `SchemaLogic.cs` | Owns snapshots, diffs, rollback and migration execution. | Primary implementation surface. |
| `cozo-om.js` | Defines Bun transactional schema migration and rollback semantics. | Observable parity reference. |
| Mission D2/D4 | Freezes additive V2 and broad snapshot preservation. | Design boundary. |

## Terms

| Term | Meaning |
|---|---|
| V2 | Additive strict-by-default schema API family. |
| preflight | Effect-free validation producing structured diagnostics and a normalized plan. |
| detect-only | Initialization recognizes legacy relation shapes but leaves them unchanged. |
