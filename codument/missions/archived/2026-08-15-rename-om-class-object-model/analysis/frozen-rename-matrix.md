# Frozen rename matrix

Task: G1-T3
Time: 2026-08-14T20:29:07Z

## Frozen low-level OM glossary

These are locked mission terms for the C# ontology OM contract and its dependent low-level projections.

| Target | Meaning | Replaces |
| --- | --- | --- |
| Class | Object category definition | Type |
| Object | Concrete persisted object data | Entity |
| Field | Class-defined storable field | Attribute |
| FieldValue | Durable value stored on an Object for a Field | Property |
| ComputedProp | Class-defined computed/getter-like member | Computed property/Computed def |
| RelationDef | Relation definition | RelDef |
| RelationLink | Explicit persisted object-to-object relation | Edge |
| Operation | Class-exposed upper-level operation | Action |
| Mutation | Low-level write building block | Existing mutation concept |
| Query | Low-level read building block | Existing query concept |

Instance remains allowed only as explanatory wording, for example "an object is an instance of a class"; it is not a primary table/API/model name.

## Frozen table rename matrix

| Old table | New table |
| --- | --- |
| `om_type` | `om_class_def` |
| `om_mixin` | `om_mixin_def` |
| `om_type_mixin` | `om_class_mixin` |
| `om_attr_def` | `om_field_def` |
| `om_attr_desc` | `om_field_desc` |
| `om_property` | `om_field_value` |
| `om_computed_def` | `om_computed_prop_def` |
| `om_entity` | `om_object` |
| `om_rel_def` | `om_relation_def` |
| `om_rel_desc` | `om_relation_desc` |
| `om_edge` | `om_relation_link` |
| `om_alias_type` | `om_alias_class` |
| `om_alias_attr` | `om_alias_field` |
| `om_alias_rel` | `om_alias_relation` |
| `om_action_def` | `om_operation_def` |
| `om_perm_action` | `om_perm_operation` |

Tables that keep their table name but rename relevant columns/JSON/docs as needed:

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

## Frozen column/API direction

Column/field names:

- `type_name` / `typeName` -> `class_name` / `className`
- `parent_type` / `parentType` -> `parent_class` / `parentClass`
- `attr_name` / `attrName` -> `field_name` / `fieldName`
- `entity_id` / `entityId` -> `object_id` / `objectId`
- `rel_name` / `relName` -> `relation_name` / `relationName`
- `from_type` / `fromType` -> `from_class` / `fromClass`
- `to_type` / `toType` -> `to_class` / `toClass`
- `from_id` / `fromId` -> `from_object_id` / `fromObjectId`
- `to_id` / `toId` -> `to_object_id` / `toObjectId`
- `value_type` / `valueType` -> `value_kind` / `valueKind`
- `constraint_type` / `constraintType` -> `constraint_kind` / `constraintKind`
- `resource_type` / `resourceType` -> `resource_class` / `resourceClass`
- `owner_type` / `ownerType` -> `owner_class` / `ownerClass`
- `action_name` / `actionName` -> `operation_name` / `operationName`
- permission/policy `action` -> `operation`
- relation-link `props` -> `payload`

Representative API names:

- `DefineTypeAsync` -> `DefineClassAsync`
- `CreateEntityAsync` -> `CreateObjectAsync`
- `UpsertEntityAsync` -> `UpsertObjectAsync`
- `DefineAttributeAsync` -> `DefineFieldAsync`
- `SetPropertyAsync` -> `SetFieldValueAsync`
- `GetPropertyAsync` -> `GetFieldValueAsync`
- `GetPropertyHistoryAsync` -> `GetFieldValueHistoryAsync`
- `FindByTypeAsync` -> `FindByClassAsync`
- `GetTypeHierarchyAsync` -> `GetClassHierarchyAsync`
- `ResolveTypeAsync` -> `ResolveClassAsync`
- `ResolveAttributeAsync` -> `ResolveFieldAsync`
- `DefineActionAsync` -> `DefineOperationAsync`
- `ExecuteActionAsync` -> `ExecuteOperationAsync`
- `RegisterAction` -> `RegisterOperation`
- `OmActionContext` -> `OmOperationContext`
- `ActionHandler` -> `OperationHandler`
- `callParentAction` -> `callParentOperation`

## Non-negotiable compatibility rule

No compatibility aliases or adapters for the old OM public/storage names. The implementation tracks should replace names directly and update tests/docs to the new contract.

## Knowledge-base skills caveat

Low-level Cozo OM projection vocabulary inside knowledge-base skills follows this frozen matrix.

High-level ontology XML DSL terms such as `ObjectType`, `BusinessObject`, `Property`, `ComputedProperty`, `Action`, and `TypeSystem` require scoped review in the knowledge-base migration track. They may be retained as XML language concepts only if the track documents a clear boundary between exchange-language terminology and low-level OM Class/Field/Operation projection terminology.
