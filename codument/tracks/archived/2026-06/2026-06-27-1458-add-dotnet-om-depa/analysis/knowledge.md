# Knowledge Context

## Source Notes
| Source | Summary | Relevance |
|--------|---------|-----------|
| `cozo-lib-dotnet/src/CozoDb.cs` | Thin managed wrapper over native Cozo operations returning raw JSON or `JsonDocument`. | Base effect provider for the new OM support layer. |
| `cozo-lib-dotnet/Cozo.DotNet.csproj` | Uses `src/**/*.cs` compile include and packages native runtimes. | Confirms `src/Om/` placement needs no csproj compile-item edit. |
| `cozo-lib-bun/cozo-om.js` | Existing Node/Bun ontology layer and behavior reference. | Defines parity target for C# OM behavior. |
| `cozo-lib-bun/cozo-om.d.ts` | Public API and data shapes for Node OM. | Source of C# model/input/result parity. |
| DEPA skill docs | Defines contract/logic/support separation, explicit runtime, capsule boundary, and fact-source rules. | Architecture guide for the C# implementation. |

## Codebase Knowledge
- `cozo-lib-dotnet` currently has no OM abstraction; it exposes database operations only.
- `CozoDb` is disposable and guards closed access. Any `ICozoOmStore` implementation should preserve that lifecycle by wrapping an existing `CozoDb`, not owning hidden global state.
- Node OM stores its schema and data in Cozo relations such as `om_type`, `om_attr_def`, `om_rel_def`, `om_entity`, `om_property`, `om_edge`, and additional metadata relations for permissions, schema versioning, and existential rules.
- Node OM's existential rule subsystem is declarative JSON and persists in `om_existential_rule_def`, which makes it suitable for parity in C# without callback serialization.

## Domain Knowledge
- DEPA core formula: `output = fn(runtime, input, config)`.
- Runtime is a data carrier for long-lived dependencies and state; business methods belong in logic functions, not on runtime classes.
- Contract/logic/support dependency direction: contract defines interfaces and models; logic depends on contract; support implements effects and calls Cozo.
- In the OM domain, TBox definitions and ABox assertions are authoritative facts in Cozo. Entity views, graph visuals, schema snapshots, and diffs are derived outputs.

## Terms
| Term | Meaning |
|------|---------|
| OM | Ontology Model / ontology management layer built above Cozo relations. |
| DEPA capsule | Self-contained module with stable public entry, hidden internals, explicit runtime/input/config logic, and isolated support effects. |
| Store contract | C# interface that represents CozoScript read/write effects required by OM logic. |
| Facade | Ergonomic public API (`CozoOm`) that delegates to logic functions without owning business behavior. |
| Parity | Matching the existing Node OM behavior and semantics unless this track states a deliberate difference. |
