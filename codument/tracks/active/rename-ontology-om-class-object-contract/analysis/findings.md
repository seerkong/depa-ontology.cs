# Findings

## Found Facts

- Mission `rename-om-class-object-model` froze the low-level OM vocabulary as Class, Object, Field, FieldValue, ComputedProp, RelationDef, RelationLink, Operation, Mutation, and Query.
- Ontology core has broad Type/Entity/Attribute/Property usage across public APIs, model DTOs, storage schema, logic, tests, and example server.
- Current durable schema creation lives in `src/Depa.Ontology/Om.Core/Logic/SchemaLogic.cs`.
- Current public API surface lives in `src/Depa.Ontology/Om.Core/CozoOm.cs`.
- Current core model DTOs live in `src/Depa.Ontology/Om.Core/Contracts/Models/OmModels.cs`.
- Current object/property retrieval merges durable property values with computed values in entity logic, so FieldValue and ComputedProp must remain conceptually distinct in the redesigned contract.

## Constraints

- No compatibility aliases or adapters for old Type/Entity/Attribute/Property public/storage names.
- This track covers only the Class/Object/Field/FieldValue storage and API contract slice.
- RelationLink, ComputedProp, and Operation behavior surfaces are reserved for the next mission track.
- Historical Codument archived records are not migration targets for this track.

## Open Questions

- None blocking. The low-level rename matrix is already frozen by mission decisions.

## Conclusions

- This track should update schema table names, column names, C# API names, DTO/model names, tests, and example usage in one breaking slice.
- Verification must include build/tests and textual scans for old public/storage names in current source, tests, examples, and docs.

## Task T1.1 spot-check

Time: 2026-08-14T20:39:51Z

- Modified files are limited to ontology tests:
  - `tests/Depa.Ontology.Tests/ExistentialGovernanceParityFixtures.cs`
  - `tests/Depa.Ontology.Tests/OmScriptingJintContractTests.cs`
  - `tests/Depa.Ontology.Tests/PermissionGovernanceParityFixtures.cs`
  - `tests/Depa.Ontology.Tests/Program.cs`
  - `tests/Depa.Ontology.Tests/PublicSurfaceParityFixtures.cs`
  - `tests/Depa.Ontology.Tests/SchemaEvolutionParityFixtures.cs`
- Diff shape: 456 insertions and 456 deletions across those 6 files.
- Public CozoOm old API scan for this slice no longer finds `DefineTypeAsync`, `CreateEntityAsync`, `UpsertEntityAsync`, `DefineAttributeAsync`, `GetPropertyHistoryAsync`, `FindByTypeAsync`, `GetTypeHierarchyAsync`, `ResolveTypeAsync`, `ResolveAttributeAsync`, `DefineTypeAliasAsync`, or `DefineAttributeAliasAsync` in ontology test files.
- Remaining `GetPropertyAsync` / `SetPropertyAsync` occurrences are in `ctx` / `context` callback scenarios and are intentionally left for the later ComputedProp/Operation track.
- `dotnet build tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore -v minimal` returns expected-red. First errors are missing production APIs such as `DefineClassAsync`, `DefineFieldAsync`, `DefineClassAliasAsync`, `DefineFieldAliasAsync`, `CreateObjectAsync`, `SetFieldValueAsync`, and missing model types such as `ClassParentPatch`.

## Task T1.2 spot-check

Time: 2026-08-14T20:44:07Z

- Modified files for this task:
  - `tests/Depa.Ontology.Tests/SchemaEvolutionParityFixtures.cs`
  - `tests/Depa.Ontology.Tests/Program.cs`
- Schema evolution fixtures now expect the target Class/Object/Field/FieldValue contract for this slice:
  - `om_type` -> `om_class_def`
  - `om_property` -> `om_field_value`
  - `om_entity` -> `om_object`
  - `typeName` / `attrName` / `valueType` schema-step fixture fields -> `className` / `fieldName` / `valueKind`
  - `parent_type` / `entity_id` / `attr_name` fixture columns -> `parent_class` / `object_id` / `field_name`
  - diagnostics in this slice now use field/object wording such as `field_value_kind_incompatible`, `required_field_value_missing`, and `rollback_object_invalid`.
- Direct Cozo fixture writes in `Program.cs` were updated for class definition, object rows, and field-value rows.
- Scope intentionally retained:
  - `om_edge`, `fromType`, and `toType` remain for the later RelationLink track.
  - `om_computed_def`, `om_action_def`, `om_mutation_def`, `om_interceptor_def`, and behavior/permission owner columns remain for the later ComputedProp/Operation/governance track.
  - Historical Codument archives were not scanned or edited.
- Scan command:
  - `rg -n "om_(type|mixin|type_mixin|attr_def|attr_desc|property|entity|alias_type|alias_attr)|\\btype_name\\b|\\bparent_type\\b|\\battr_name\\b|\\bentity_id\\b|\\bvalue_type\\b|addType|addAttribute|changeAttribute|typeName|attrName|valueType|attribute_value_type_incompatible|required_property_missing|rollback_entity_invalid|TypeExistsAsync" tests/Depa.Ontology.Tests/SchemaEvolutionParityFixtures.cs tests/Depa.Ontology.Tests/Program.cs`
- Scan result:
  - No remaining `om_type`, `om_property`, `om_entity`, `parent_type`, `entity_id`, `attr_name`, `value_type`, `addType`, `addAttribute`, `changeAttribute`, `typeName`, `attrName`, `valueType`, or old schema diagnostic code hits in `SchemaEvolutionParityFixtures.cs`.
  - Remaining hits in `Program.cs` are behavior/computed/operation/constraint-related or public-view surface names outside this T1.2 fixture slice.
- Build command:
  - `dotnet build tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore -v minimal`
- Build result:
  - Expected red: command exits 1 with 439 compile errors because production code has not yet introduced the new Class/Object/Field/FieldValue APIs and model types. First errors remain missing `DefineClassAsync`, `DefineFieldAsync`, `DefineClassAliasAsync`, `DefineFieldAliasAsync`, `CreateObjectAsync`, `SetFieldValueAsync`, and `ClassParentPatch`.

## Task T2.1 spot-check

Time: 2026-08-14T20:53:06Z

- Modified production files for this task:
  - `src/Depa.Ontology/Om.Core/CozoOm.cs`
  - `src/Depa.Ontology/Om.Core/Contracts/Models/OmModels.cs`
  - `src/Depa.Ontology/Om.Core/Inputs/OmInputs.cs`
  - `src/Depa.Ontology/Om.Core/Logic/TypeLogic.cs`
  - `src/Depa.Ontology/Om.Core/Logic/EntityLogic.cs`
  - `src/Depa.Ontology/Om.Core/Logic/RelationLogic.cs`
  - `src/Depa.Ontology/Om.Analytics/CozoOmAnalyticsExtensions.cs`
- Modified tests/call sites only where needed to compile against the new public surface:
  - `tests/Depa.Ontology.Tests/Program.cs`
  - `tests/Depa.Ontology.Tests/PublicSurfaceParityFixtures.cs`
  - `tests/Depa.Ontology.Tests/SchemaEvolutionParityFixtures.cs`
  - `tests/Depa.Ontology.Tests/OmScriptingJintContractTests.cs`
- Public `CozoOm` surface now exposes this slice through Class/Object/Field/FieldValue names:
  - `DefineClassAsync`, `DefineFieldAsync`, `DefineClassAliasAsync`, `DefineFieldAliasAsync`
  - `CreateObjectAsync`, `UpsertObjectAsync`, `GetObjectClassAsync`, `DeleteObjectAsync`
  - `SetFieldValueAsync`, `GetFieldValueAsync`, `GetFieldValueAsOfAsync`, `GetFieldValueHistoryAsync`
  - `FindByClassAsync`, `FindByClassWithFieldValuesAsync`, `AggregateByClassAsync`
  - `GetClassHierarchyAsync`, `ResolveClassAsync`, `ResolveFieldAsync`, `GetFieldDefinitionsAsync`
  - `ValidateObjectAsync`, `ValidateFieldValueTypeAsync`, `ValidateRequiredFieldValuesAsync`, `FinalizeObjectAsync`
- Public model DTOs now expose this slice through new names:
  - `OmClass`, `ClassParentPatch`, `ClassParentPatchKind`, `DefineClassPatchInput`
  - `OmField`, `OmObject`, `OmFieldValue`, `ObjectView`, `ObjectViewEdge`
  - `FindByClassEntry`, `FieldValueHistoryEntry`, `ClassHierarchyNode`, `ClassHierarchy`
- Old Type/Entity/Attribute/Property DTOs remain only as internal implementation records for this temporary storage-rename boundary; they are not exposed through `CozoOm`.
- Public API scan command:
  - `rg -n "public .*\\b(DefineTypeAsync|CreateEntityAsync|UpsertEntityAsync|DefineAttributeAsync|SetPropertyAsync|GetPropertyAsync|GetPropertyAsOfAsync|GetPropertyHistoryAsync|FindByTypeAsync|FindByTypeWithPropertiesAsync|AggregateByTypeAsync|GetTypeHierarchyAsync|ResolveTypeAsync|ResolveAttributeAsync|GetAttributeDefinitionsAsync|ValidateEntityAsync|ValidatePropertyTypeAsync|ValidateRequiredPropertiesAsync|FinalizeEntityAsync|GetEntityTypeAsync|GetEntityViewAsync|GetEntityViewAsOfAsync|IsSubtypeOfAsync)\\b" src/Depa.Ontology/Om.Core/CozoOm.cs`
- Public API scan result:
  - No hits in `CozoOm.cs` for the old public API names in this slice.
- Build command:
  - `dotnet build tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore -v minimal`
- Build result:
  - Passed with 0 warnings and 0 errors.
- Remaining expected work:
  - T2.2/T2.3 must rename storage tables, columns, logic queries, aliases, hierarchy/mixin internals, and search paths. Internal old DTO/input names are intentionally left as a temporary boundary for those tasks.
  - RelationLink, ComputedProp, and Operation/Action surfaces remain out of this track slice and will be handled by the next mission track.

## Task T2.2 implementation note

Time: 2026-08-14T21:00:23Z

- Modified production file:
  - `src/Depa.Ontology/Om.Core/Logic/SchemaLogic.cs`
- Schema initialization now creates the Class/Object/Field/FieldValue storage relations for this slice:
  - `om_class_def {class_name => description, parent_class}`
  - `om_mixin_def`
  - `om_class_mixin {class_name, mixin_name}`
  - `om_field_def {class_name, field_name => value_kind, required}`
  - `om_field_desc {class_name, field_name => description}`
  - `om_object {id => class_name, label}`
  - `om_field_value {object_id, field_name, valid_time => value, tx_time}`
  - `om_alias_class`
  - `om_alias_field {class_name, alias_field => canonical_field}`
- Schema migration step parsing now accepts only this slice's new current names:
  - `addClass`, `addField`, `renameField`, `changeField`
  - `className` / `class_name`, `parentClass` / `parent_class`, `fieldName` / `field_name`, `fromField` / `from_field`, `toField` / `to_field`, `valueKind` / `value_kind`
  - It no longer falls back through the old schema-step discriminator field name `type`.
- Schema V2 preflight and rollback diagnostics now use Object/Field/FieldValue wording and codes for this slice:
  - `field_value_kind_incompatible`
  - `required_field_value_missing`
  - `rollback_object_invalid`
- Schema snapshot, keyed diff, and rollback relation specs now emit/read the renamed current-slice tables and columns.
- Legacy temporal detection/upgrade for durable field values now targets `om_field_value` and `object_id` / `field_name`; `om_edge` remains unchanged for the later RelationLink track.
- Intentionally retained out-of-scope schema names:
  - `om_rel_def`, `om_rel_desc`, `om_edge`, `from_type`, `to_type`, `from_id`, `to_id`, `props`
  - `om_computed_def`, `om_action_def`, `om_mutation_def`, `om_interceptor_def`, `om_perm_action`
  - Behavior/computed/action/constraint owner columns such as `type_name`, `attr_name`, and `action_name` remain for the later ComputedProp/Operation/governance tracks.
- Scan command:
  - `rg -n "\\bom_type\\b|\\bom_mixin\\b|\\bom_type_mixin\\b|\\bom_attr_def\\b|\\bom_attr_desc\\b|\\bom_property\\b|\\bom_entity\\b|\\bom_alias_type\\b|\\bom_alias_attr\\b|typeName|parentType|attrName|entityId|valueType|addType|addAttribute|renameAttribute|changeAttribute|required_property_missing|attribute_value_type_incompatible|rollback_entity_invalid" src/Depa.Ontology/Om.Core/Logic/SchemaLogic.cs`
- Scan result:
  - No hits for old current-slice table names, schema-step JSON names, or old current-slice schema diagnostic codes in `SchemaLogic.cs`.
- Build command:
  - `dotnet build tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore -v minimal`
- Build result:
  - Passed with 0 warnings and 0 errors.
- Remaining expected work:
  - T2.3 must update logic queries, aliases, hierarchy, mixin internals, and object search paths to read/write these new storage relations directly.
  - RelationLink, ComputedProp, and Operation/Action surfaces remain out of this track slice.

## Task T2.3 implementation note

Time: 2026-08-14T21:08:01Z

- Modified production files for this task:
  - `src/Depa.Ontology/Om.Core/Logic/TypeLogic.cs`
  - `src/Depa.Ontology/Om.Core/Logic/EntityLogic.cs`
  - `src/Depa.Ontology/Om.Core/Logic/RelationLogic.cs`
  - `src/Depa.Ontology/Om.Core/Logic/ExistentialRuleLogic.cs`
- Modified test file:
  - `tests/Depa.Ontology.Tests/Program.cs`
- Logic queries now read/write the current-slice storage contract:
  - `om_class_def {class_name => description, parent_class}`
  - `om_mixin_def`
  - `om_class_mixin {class_name, mixin_name}`
  - `om_field_def {class_name, field_name => value_kind, required}`
  - `om_field_desc {class_name, field_name => description}`
  - `om_object {id => class_name, label}`
  - `om_field_value {object_id, field_name, valid_time => value, tx_time}`
  - `om_alias_class`
  - `om_alias_field {class_name, alias_field => canonical_field}`
- Neighbor traversal and existential-rule search paths now join object rows through `om_object` and field values through `om_field_value`.
- Existential-rule class alias expansion now reads `om_alias_class`; relation alias expansion remains on `om_alias_rel` because RelationLink is out of this track slice.
- Production error messages touched by this slice now use Class/Object/Field wording for missing class, parent class, field value kind, and class/field alias cycles.
- Runtime smoke initially exposed two real storage-path misses and one test-contract miss:
  - `om_class_def` was first queried by old key `name`; fixed to `class_name`.
  - existential rules still queried `om_alias_type`, `om_entity`, and `om_property`; fixed to the new class/object/field-value relations.
  - two missing-class assertions in `Program.cs` still expected old Type wording; fixed to Class/Object wording.
- Build command:
  - `dotnet build tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore -v minimal`
- Build result:
  - Passed with 0 warnings and 0 errors.
- Runtime smoke command:
  - `dotnet run --project tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore`
- Runtime smoke result:
  - Passed: `Depa.Ontology integration tests passed.`
- Scan command:
  - `rg -n "om_(type|type_mixin|attr_def|attr_desc|property|entity|alias_type|alias_attr)|\\bparent_type\\b|\\bentity_id\\b|\\battr_name\\b|\\bvalue_type\\b|\\btype_name\\b" src/Depa.Ontology/Om.Core/Logic/TypeLogic.cs src/Depa.Ontology/Om.Core/Logic/EntityLogic.cs src/Depa.Ontology/Om.Core/Logic/RelationLogic.cs src/Depa.Ontology/Om.Core/Logic/ExistentialRuleLogic.cs`
- Scan result:
  - No current-slice old storage table/column hits remain in `EntityLogic.cs`, `RelationLogic.cs`, or `ExistentialRuleLogic.cs`.
  - Remaining hits in `TypeLogic.cs` are `om_rel_def` plus `from_type` / `to_type`, which are intentionally reserved for the later RelationLink track.
- Remaining expected work:
  - P3 must update examples/docs in this track.
  - P4 must perform final build/test/scan verification and record the implementation report.
  - RelationLink, ComputedProp, and Operation/Action surfaces remain reserved for the next mission track.

## Task T3.1 implementation note

Time: 2026-08-14T21:13:27Z

- Modified example server files for this task:
  - `examples/Depa.Ontology.ExampleServer/DemoRunner.cs`
  - `examples/Depa.Ontology.ExampleServer/OmServerState.cs`
  - `examples/Depa.Ontology.ExampleServer/DemoCatalog.cs`
- Example server current-slice calls now use the new Class/Object/Field/FieldValue public API:
  - `DefineClassAsync`
  - `DefineFieldAsync`
  - `CreateObjectAsync`
  - `SetFieldValueAsync`
  - `GetFieldValueAsync`
  - `GetFieldValueAsOfAsync`
  - `FindByClassAsync`
  - `GetClassHierarchyAsync`
  - `GetObjectClassAsync`
- Demo seed/table wording for this slice now uses:
  - `Class 定义` with `className` / `parentClass`
  - `Field 定义` with `className` / `fieldName` / `valueKind`
  - `Object 数据` with `className`
  - `FieldValue 数据` with `objectId` / `fieldName`
- The example runner creates objects and field values through the new public API instead of using old batch entity/property DTOs. Relation edge ingestion remains on the current relation API because RelationLink is reserved for the next mission track.
- Build command:
  - `dotnet build examples/Depa.Ontology.ExampleServer/Depa.Ontology.ExampleServer.csproj --no-restore -v minimal`
- Build result:
  - Passed with 0 warnings and 0 errors.
- Scan command:
  - `rg -n "DefineTypeAsync|CreateEntityAsync|UpsertEntityAsync|DefineAttributeAsync|SetPropertyAsync|GetPropertyAsync|GetPropertyAsOfAsync|GetPropertyHistoryAsync|FindByTypeAsync|FindByTypeWithPropertiesAsync|AggregateByTypeAsync|GetTypeHierarchyAsync|ResolveTypeAsync|ResolveAttributeAsync|DefineTypeAliasAsync|DefineAttributeAliasAsync|\btypeName\b|\bparentType\b|\battrName\b|\bentityId\b|\bvalueType\b|类型定义|属性定义|实体数据|属性数据|DemoAttribute|DemoEntity|DemoProperty|TypeName|AttrName|EntityId|Properties" examples/Depa.Ontology.ExampleServer`
- Scan result:
  - No old current-slice public API calls or demo seed/table names remain.
  - One remaining `EntityId` hit is `NeighborEntry.EntityId` accessed from relation-neighbor traversal in `DemoRunner.cs`; it is intentionally left for the later RelationLink track.

## Task T3.2 implementation note

Time: 2026-08-14T21:15:00Z

- Modified current example/doc files for this task:
  - `examples/Depa.Ontology.ExampleServer.Tests/Program.cs`
  - `examples/Depa.Ontology.ExampleServer/README.md`
  - `codument/behaviors/dotnet-example-server.xml`
- Example server HTTP contract test schema migration now uses the renamed schema-step contract:
  - `addClass`
  - `className`
- Example server README now documents the Class/Object/Field/FieldValue demo table contract and notes that the browser version must speak the same renamed contract.
- The durable example-server behavior registry was updated from "current example-browser" wording to "matching version example-browser" wording so it does not promise old table vocabulary compatibility after this breaking rename.
- Build commands:
  - `dotnet build examples/Depa.Ontology.ExampleServer/Depa.Ontology.ExampleServer.csproj --no-restore -v minimal`
  - `dotnet build examples/Depa.Ontology.ExampleServer.Tests/Depa.Ontology.ExampleServer.Tests.csproj --no-restore -v minimal`
- Build results:
  - Both passed with 0 warnings and 0 errors.
- Runtime contract command:
  - `dotnet run --project examples/Depa.Ontology.ExampleServer.Tests/Depa.Ontology.ExampleServer.Tests.csproj --no-restore`
- Runtime contract result:
  - Passed: `Example server HTTP contract tests passed.`
- Scan command:
  - `rg -n "DefineTypeAsync|CreateEntityAsync|UpsertEntityAsync|DefineAttributeAsync|SetPropertyAsync|GetPropertyAsync|GetPropertyAsOfAsync|GetPropertyHistoryAsync|FindByTypeAsync|FindByTypeWithPropertiesAsync|AggregateByTypeAsync|GetTypeHierarchyAsync|ResolveTypeAsync|ResolveAttributeAsync|DefineTypeAliasAsync|DefineAttributeAliasAsync|\btypeName\b|\bparentType\b|\battrName\b|\bentityId\b|\bvalueType\b|addType|addAttribute|changeAttribute|类型定义|属性定义|实体数据|属性数据" examples/Depa.Ontology.ExampleServer examples/Depa.Ontology.ExampleServer.Tests examples/Depa.Ontology.ExampleServer/README.md codument/behaviors/dotnet-example-server.xml`
- Scan result:
  - No hits.
