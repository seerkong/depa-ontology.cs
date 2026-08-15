# Knowledge Context

## Source Notes

| Source | Summary | Relevance |
|--------|---------|-----------|
| `codument/missions/active/rename-om-class-object-model/design.md` | Freezes new vocabulary and table/API rename matrix. | This track implements the second ontology-core slice. |
| `codument/tracks/active/rename-ontology-om-class-object-contract/analysis/findings.md` | Records completed Class/Object/Field/FieldValue rename and remaining out-of-scope names. | Establishes the starting point and known residual vocabulary. |

## Codebase Knowledge

- Schema creation and schema snapshot/diff logic live in `src/Depa.Ontology/Om.Core/Logic/SchemaLogic.cs`.
- Public ontology API facade lives in `src/Depa.Ontology/Om.Core/CozoOm.cs`.
- DTOs/models live in `src/Depa.Ontology/Om.Core/Contracts/Models/OmModels.cs`.
- Runtime callback context lives in `src/Depa.Ontology/Om.Core/Runtime/CozoOmRuntime.cs`.
- Behavior/governance surfaces include behavior import/catalog logic, constraint logic, permission metadata, operation registry, and example server contracts.

## Domain Knowledge

- RelationLink means an explicit persisted object-to-object link, not a derived/inferred relation result.
- Operation means a Class-exposed upper-level operation; Mutation and Query remain lower-level building blocks.
- ComputedProp is deliberately not FieldValue because it is computed getter-like behavior rather than durable object-held data.

## Terms

| Term | Meaning |
|------|---------|
| RelationDef | Definition of a relation between Classes. |
| RelationLink | Persisted object-to-object link. |
| ComputedProp | Computed getter-like member defined by a Class. |
| Operation | Class-exposed upper-level operation. |
