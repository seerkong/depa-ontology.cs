# Knowledge Context

## Source Notes

| Source | Summary | Relevance |
|---|---|---|
| `cozo-lib-bun/cozo-om.js` | Reference parent dispatch and inherited interceptor collection | Defines parity semantics |
| `cozo-lib-dotnet/src/Om.Core/Logic/ConstraintLogic.cs` | Transaction, action/mutation lookup, interceptor collection, behavior metadata | Main implementation boundary |
| `cozo-lib-dotnet/src/Om.Core/Runtime/CozoOmRuntime.cs` | Typed callback registry and action context | Parent API and registration state |
| `cozo-lib-dotnet/tests/Program.cs` | Existing action transaction and rollback tests | C# regression harness |

## Codebase Knowledge

- `ActionOwnerType` is different from the entity's concrete `TypeName` and is the correct parent-dispatch cursor.
- Interceptor `Seq` values are local to `(type, action, phase)`, so they cannot be globally compared across owners.
- The facade owns convenience overloads while Logic owns domain validation and Store effects.

## Domain Knowledge

- Parent-action dispatch is method-composition semantics, not recursive full action execution.
- Behavior metadata readiness is distinct from callback execution readiness; full portability belongs to a later track.

## Terms

| Term | Meaning |
|---|---|
| Action owner | Type whose registry entry supplied the currently executing action handler |
| Concrete type | Actual type stored for the target entity |
| Ghost callback | Registry callback left behind when its persistent definition failed |
