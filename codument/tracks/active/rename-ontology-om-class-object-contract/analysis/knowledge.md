# Knowledge Context

## Source Notes

| Source | Summary | Relevance |
|--------|---------|-----------|
| `codument/missions/active/rename-om-class-object-model/design.md` | Mission-level glossary and table/API rename matrix. | Primary design authority. |
| `codument/missions/active/rename-om-class-object-model/analysis/ontology-core-inventory.md` | Counts and representative files for legacy OM vocabulary in ontology core. | Scope evidence. |
| `src/Depa.Ontology/Om.Core/Logic/SchemaLogic.cs` | Creates and evolves current OM durable relations. | Storage rename authority. |
| `src/Depa.Ontology/Om.Core/CozoOm.cs` | Exposes current public API. | Public contract rename authority. |
| `src/Depa.Ontology/Om.Core/Contracts/Models/OmModels.cs` | Defines current DTO/model names. | Model rename authority. |

## Codebase Knowledge

- `TypeLogic` owns type hierarchy, mixin, attribute, relation definition, and alias logic today.
- `EntityLogic` owns entity creation/upsert/delete, property write/read/history, object views, and search by type today.
- `SchemaLogic` stores both canonical current schema and schema evolution/diff logic.
- Tests include both runtime behavior tests and schema-evolution compatibility fixtures that mention legacy table names.

## Domain Knowledge

- Class names define object categories.
- Objects are concrete persisted instances of a Class.
- Fields are durable storable members declared on Classes.
- FieldValue rows store Object-held values for Fields.
- ComputedProp is not a FieldValue and should be handled in the later computed/operation track.

## Terms

| Term | Meaning |
|------|---------|
| Class | Object category definition, replacing Type. |
| Object | Concrete persisted object, replacing Entity. |
| Field | Class-defined storable member, replacing Attribute. |
| FieldValue | Durable Object-held value, replacing Property. |
