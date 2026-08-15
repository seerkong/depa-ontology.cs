# 变更：Rename ontology OM Class/Object/Field contract

## 背景和动机 (Context And Why)

The ontology OM core currently exposes Type, Entity, Attribute, and Property as public and storage vocabulary. The mission `rename-om-class-object-model` freezes a new terminology system because the old words are too broad and conflict with C# and ORM conventions.

This track implements the first ontology-core slice: Class/Object/Field/FieldValue storage and public contract.

## "要做"和"不做" (Goals / Non-Goals)

目标:

- **BREAKING** rename Type concepts to Class in public API, models, schema tables, columns, tests, examples, and docs.
- **BREAKING** rename Entity concepts to Object.
- **BREAKING** rename Attribute concepts to Field.
- **BREAKING** rename durable Property values to FieldValue.
- Update schema evolution, snapshots, import steps, aliases, mixins, hierarchy, search, and example usage that belong to this slice.
- Remove legacy compatibility aliases and old storage names from current code.

非目标:

- Do not handle RelationLink, ComputedProp, or Operation behavior surfaces in this track.
- Do not migrate the dependent knowledge-base project in this track.
- Do not rewrite historical Codument archives.
- Do not add adapters for old API/table names.

## 变更内容（What Changes）

- Public API examples:
  - `DefineTypeAsync` -> `DefineClassAsync`
  - `CreateEntityAsync` -> `CreateObjectAsync`
  - `UpsertEntityAsync` -> `UpsertObjectAsync`
  - `DefineAttributeAsync` -> `DefineFieldAsync`
  - `SetPropertyAsync` -> `SetFieldValueAsync`
  - `GetPropertyAsync` -> `GetFieldValueAsync`
  - `FindByTypeAsync` -> `FindByClassAsync`
- Storage examples:
  - `om_type` -> `om_class_def`
  - `om_mixin` -> `om_mixin_def`
  - `om_type_mixin` -> `om_class_mixin`
  - `om_attr_def` -> `om_field_def`
  - `om_property` -> `om_field_value`
  - `om_entity` -> `om_object`
- Column examples:
  - `type_name` -> `class_name`
  - `parent_type` -> `parent_class`
  - `attr_name` -> `field_name`
  - `entity_id` -> `object_id`
  - `value_type` -> `value_kind`

## 影响范围（Impact）

- 受影响的能力（behaviors）: `cozo-dotnet-om`
- 受影响的代码:
  - `src/Depa.Ontology/Om.Core/CozoOm.cs`
  - `src/Depa.Ontology/Om.Core/Contracts/Models/OmModels.cs`
  - `src/Depa.Ontology/Om.Core/Logic/SchemaLogic.cs`
  - `src/Depa.Ontology/Om.Core/Logic/TypeLogic.cs`
  - `src/Depa.Ontology/Om.Core/Logic/EntityLogic.cs`
  - `src/Depa.Ontology/Om.Batch`
  - `src/Depa.Ontology/Om.Analytics`
  - `examples/Depa.Ontology.ExampleServer`
  - `tests/Depa.Ontology.Tests`
