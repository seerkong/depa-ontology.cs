# Mission design: OM naming redesign

## Why this mission exists

The project currently contains an object modeling layer over Cozo. Historical naming uses Type, Entity, Attribute, Property, Edge, and Action. During design discussion, these names were found to be too overloaded:

- Type is too broad and collides with ordinary programming type vocabulary.
- Entity may be confused with ORM entity modeling.
- Property in C# often means a getter/setter member rather than a persisted field value.
- Action is narrower and less compositional than the intended class-exposed operation concept.
- Edge sounds graph-technical; the user prefers an explicit relation-link term for persisted object-to-object connections.

The new language intentionally names the modeling domain instead of mirroring generic implementation vocabulary.

## Final glossary

| New term | Meaning | Old term displaced |
| --- | --- | --- |
| Class | Defines an object category | Type |
| Object | Concrete object data | Entity |
| Instance | Documentation/relation wording only | Not a primary persisted concept |
| Field | Storable member defined by a Class | Attribute |
| FieldValue | Durable value stored on an Object for a Field | Property |
| ComputedProp | Computed getter-like member defined by a Class | Computed property/Computed def |
| RelationDef | Definition of a relation between Classes | RelDef |
| RelationLink | Explicit persisted object-to-object relation | Edge |
| Operation | Class-exposed upper-level operation | Action |
| Mutation | Low-level write building block | Existing mutation concept |
| Query | Low-level read building block | Existing query concept |

## Table rename matrix

| Old table | New table | Notes |
| --- | --- | --- |
| `om_type` | `om_class_def` | Class definition table |
| `om_mixin` | `om_mixin_def` | Mixin definition table; renamed for suffix consistency |
| `om_type_mixin` | `om_class_mixin` | Class-to-mixin binding |
| `om_attr_def` | `om_field_def` | Class-defined storable fields |
| `om_attr_desc` | `om_field_desc` | Field description metadata |
| `om_property` | `om_field_value` | Object-held durable field values |
| `om_computed_def` | `om_computed_prop_def` | ComputedProp definition, not ComputedField |
| `om_entity` | `om_object` | Concrete persisted object |
| `om_rel_def` | `om_relation_def` | User-approved relation definition name |
| `om_rel_desc` | `om_relation_desc` | Relation description metadata |
| `om_edge` | `om_relation_link` | Explicit persisted relation link; avoids relation-fact wording |
| `om_alias_type` | `om_alias_class` | Class alias |
| `om_alias_attr` | `om_alias_field` | Field alias |
| `om_alias_rel` | `om_alias_relation` | Relation alias |
| `om_action_def` | `om_operation_def` | Class-exposed operation definition |
| `om_perm_action` | `om_perm_operation` | Permission operation vocabulary follows action rename |

Tables expected to keep their current table names while changing relevant columns, JSON fields, and docs:

- `om_constraint_def`
- `om_mutation_def`
- `om_interceptor_def`
- `om_behavior_binding`
- `om_perm_policy`
- `om_perm_abac_rule`
- `om_perm_path_rule`
- `om_schema_state`
- `om_schema_version`
- `om_schema_migration`
- `om_schema_snapshot`
- `om_existential_rule_def`

## Column and field rename matrix

| Old name | New name |
| --- | --- |
| `type_name`, `typeName` | `class_name`, `className` |
| `parent_type` | `parent_class` |
| `attr_name`, `attrName` | `field_name`, `fieldName` |
| `entity_id` | `object_id` |
| `rel_name` | `relation_name` |
| `from_type` | `from_class` |
| `to_type` | `to_class` |
| `from_id` | `from_object_id` |
| `to_id` | `to_object_id` |
| `value_type` | `value_kind` |
| `constraint_type` | `constraint_kind` |
| `resource_type` | `resource_class` |
| `owner_type` | `owner_class` |
| `action_name` | `operation_name` |
| `action` in permission/action contexts | `operation` |
| relation-link `props` | `payload` |

The `kind` naming is preferred where a column describes a scalar/category discriminator instead of an OM Class.

## API and model rename matrix

| Old API/model | New API/model |
| --- | --- |
| `DefineTypeAsync` | `DefineClassAsync` |
| `CreateEntityAsync` | `CreateObjectAsync` |
| `UpsertEntityAsync` | `UpsertObjectAsync` |
| `DefineAttributeAsync` | `DefineFieldAsync` |
| `SetPropertyAsync` | `SetFieldValueAsync` |
| `GetPropertyAsync` | `GetFieldValueAsync` |
| `GetPropertyHistoryAsync` | `GetFieldValueHistoryAsync` |
| `FindByTypeAsync` | `FindByClassAsync` |
| `GetTypeHierarchyAsync` | `GetClassHierarchyAsync` |
| `ResolveTypeAsync` | `ResolveClassAsync` |
| `ResolveAttributeAsync` | `ResolveFieldAsync` |
| `DefineTypeAliasAsync` | `DefineClassAliasAsync` |
| `DefineAttributeAliasAsync` | `DefineFieldAliasAsync` |
| `DefineRelationAliasAsync` | `DefineRelationAliasAsync` |
| `DefineActionAsync` | `DefineOperationAsync` |
| `ExecuteActionAsync` | `ExecuteOperationAsync` |
| `RegisterAction` | `RegisterOperation` |
| `OmActionContext` | `OmOperationContext` |
| `ActionHandler` | `OperationHandler` |
| `callParentAction` | `callParentOperation` |
| `actionOwnerType` | `operationOwnerClass` |

Model types and DTO fields should follow the same contract, for example `OmAttribute` becomes `OmField`, `OmEntity` becomes `OmObject`, `OmProperty` becomes `OmFieldValue`, and view objects should expose field values distinctly from computed props.

## Current-state anchors

The implementation tracks should inspect and update at least these source areas:

- Ontology schema creation currently lives under `src/Depa.Ontology/Om.Core/Logic/SchemaLogic.cs`.
- The ontology public API currently lives under `src/Depa.Ontology/Om.Core/CozoOm.cs`.
- Core models currently live under `src/Depa.Ontology/Om.Core/Contracts/Models/OmModels.cs`.
- Object retrieval currently merges durable properties with computed values in entity logic; the redesigned contract should avoid presenting durable FieldValue and ComputedProp as if they were the same persisted concept.
- The TypeScript predecessor package uses the same Type/Entity/Property/Action language and can be used as historical reference, but the C# mission target is the new vocabulary.
- The dependent knowledge-base project uses ontology APIs and direct OM table queries, so it must be updated after the host contract changes.
- The dependent knowledge-base project also contains project-level skills. Those skills, their references, prompt contracts, validator/audit scripts, tests, and any versioned agent-skill copies that repeat OM concepts are migration targets, not external documentation.

## Track decomposition

This mission deliberately does not perform the full rename as one giant mutation. It should create or revise tracks for bounded executable work:

1. Ontology core class/object/field storage-contract track.
2. Ontology core relation/computed/operation behavior track.
3. Knowledge-base follow-up migration track, including code, direct Cozo queries, docs, and skill assets.

If inventory shows that a track would become too broad, split it before implementation while preserving this mission's glossary and no-compatibility rule.

## Verification expectations

Each implementation track should run checks proportional to its scope. Final mission verification should include:

- ontology core build/test;
- knowledge-base build/test;
- textual scan for legacy OM-specific table/API names;
- textual scan and, where available, skill-specific tests/validators for knowledge-base skill assets;
- review of docs and Codument records that users will rely on after the rename.

## Replanning rules

Replan if an implementation track discovers a concept not covered by this design, especially:

- a public API that cannot be cleanly mapped to the new vocabulary;
- a persisted table/column whose old name would leak into the user-visible model;
- a knowledge-base dependency that needs a separate migration phase;
- a knowledge-base skill whose prompt contract or validator encodes old OM vocabulary in a way that needs a dedicated skill-migration track;
- evidence that RelationLink must be split from derived relation results sooner than expected.

Do not reintroduce legacy compatibility without explicit user approval.
