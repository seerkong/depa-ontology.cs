# Knowledge Context

## Source Notes

| Source | Summary | Relevance |
|--------|---------|-----------|
| cozo-lib-bun/cozo-om.js | Global callback maps and transaction-based action execution | Bun runtime ownership |
| cozo-lib-dotnet/src/Om.Core/Runtime/CozoOmRuntime.cs | Per-instance immutable registry snapshots and behavior gate | Reference lifecycle |
| add-dotnet-om-behavior-portability | Binding/readiness import concurrency contract | Clear/import interaction |

## Codebase Knowledge

- Bun plain runners and transaction runners are different objects.
- C# behavior resolution captures an immutable registry snapshot.

## Domain Knowledge

- A callback registry is process-local executable state, not schema state.
- Readiness must be recomputed after clear or restart.

## Terms

| Term | Meaning |
|------|---------|
| runtime owner | The single lifecycle owner of one callback registry |
| legacy adapter | Compatibility path that delegates plain-runner calls to one explicit legacy runtime |
| resolution scope | Immutable registry snapshot used for one behavior command |
