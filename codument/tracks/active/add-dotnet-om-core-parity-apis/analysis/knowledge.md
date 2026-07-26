# Knowledge Context

## Source Notes
| Source | Summary | Relevance |
|--------|---------|-----------|
| `cozo-lib-bun/cozo-om.js` | Bun OM reference implements historical entity views, property-filtered `findByType`, public validation helpers, inherited computed properties, and typed runtime constraints. | Defines target parity behavior. |
| `cozo-lib-bun/__tests__/om-temporal-entity-view-asof.test.js` | Tests `getEntityViewAsOf` for historical properties and computed properties evaluated against historical context. | Acceptance reference for C# historical entity view. |
| `cozo-lib-bun/__tests__/om-constraint-*.test.js` | Tests conditional, cross-entity, computed-dependent, and inherited constraint behavior. | Acceptance reference for C# constraint scope parity. |
| `cozo-lib-dotnet/src/Om.Core/CozoOm.cs` | Public facade exposes `GetEntityViewAsync`, `FindByTypeAsync`, `ValidateEntityAsync`, property/neighbor AsOf primitives, but lacks full parity APIs. | Primary C# API surface to extend. |
| `cozo-lib-dotnet/src/Om.Core/Logic/EntityLogic.cs` | C# has internal `ValidatePropertyTypeAsync`; `GetEntityViewAsync` assembles NOW view; `FindByTypeAsync` returns lightweight entities. | Implementation locus for entity view and type search parity. |
| `cozo-lib-dotnet/src/Om.Core/Logic/ConstraintLogic.cs` | C# validation checks required/type errors and only executes `custom` validators by exact type. | Implementation locus for typed/inherited runtime constraints. |
| `cozo-lib-dotnet/src/Om.Core/Runtime/CozoOmRuntime.cs` | Runtime registry stores validators and computed delegates by exact `(type, name)` key. | Needs inherited lookup helpers or logic-level resolution. |

## Codebase Knowledge
- `Om.Core` uses a DEPA-style split: facade in `CozoOm`, data contracts in `Contracts`, inputs in `Inputs`, logic in `Logic`, store boundary in `Support`, utilities in `Internals`.
- Logic code should use `ICozoOmStore` via `CozoOmRuntime` and must not directly depend on native Cozo interop.
- Entity views use `EntityView` and `EntityViewEdge` model types and currently include canonicalized properties plus outgoing edges.
- Stored properties and edges use Cozo validity semantics; `@ "NOW"` and `@ $as_of` are the core query distinction.
- Type hierarchy helpers already exist and can support inherited computed/constraint resolution.

## Domain Knowledge
- "Core object system parity" here means the semantic kernel around type, attribute, relation, entity view, validation, and runtime behavior; it excludes higher-level CodeKnowledge/DEPA ontology changes.
- Bun remains the reference for callback semantics, but C# public APIs should expose .NET-friendly delegates and result records.
- `finalizeEntity` should mean "assert that an entity is complete according to required properties and constraints" rather than deleting or freezing the entity.

## Terms
| Term | Meaning |
|------|---------|
| Entity view | A complete object-level read model with id, canonical type, label, properties, and outgoing edge summaries. |
| AsOf view | An entity view evaluated against a historical valid-time timestamp. |
| Filtered type search | `findByType` behavior that restricts results by current property equality and returns matched properties. |
| Conditional constraint | Runtime constraint whose `when` predicate activates and `then` predicate validates the entity. |
| Cross-entity constraint | Runtime constraint that can inspect graph neighbors or related entities. |
| Computed-dependent constraint | Runtime constraint whose predicates may depend on computed properties. |
| Finalize entity | Public API that validates required properties and registered constraints before treating an entity as complete. |
