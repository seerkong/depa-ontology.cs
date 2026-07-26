# Findings

## Found Facts

- Bun owns callbacks in five module-global maps and exports a global `clearRegistry()`.
- Bun `executeAction()` creates a transaction runner, so a naive runner-keyed WeakMap would lose callbacks registered on the root DB.
- .NET creates one `CozoOmRegistry` per `CozoOm` and publishes immutable snapshots.
- .NET behavior import and execution already coordinate through `BehaviorRuntimeGate`.

## Constraints

- Preserve legacy Bun free-function callers.
- Do not serialize callback bodies.
- Do not let clear delete persisted definitions or binding identities.
- Runtime must remain a DEPA data/dependency carrier.

## Open Questions

- None. Mission decisions D6 and D7 freeze the lifecycle and portability direction.

## Conclusions

- Bun needs an explicit runtime carrier that survives transaction-runner changes.
- C# clear must use the behavior gate rather than directly exposing an unsafe registry reset.
