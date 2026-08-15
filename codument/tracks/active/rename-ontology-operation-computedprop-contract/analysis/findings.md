# Findings

## Found Facts

- The first mission track completed public/storage Class/Object/Field/FieldValue rename and passed ontology + example runtime tests.
- Parent verification scan still found implementation-level Type/Entity/Attribute/Property names and second-slice storage names such as `om_computed_def`.
- Mission design reserves relation, computed prop, operation/action, and permission operation rename for this track.

## Constraints

- No compatibility aliases or adapters for old public API names or old table names.
- Knowledge-base migration is out of this track and belongs to mission G3.
- Historical archived Codument records are not migration targets.

## Open Questions

- None blocking. The vocabulary is already frozen by mission design and user decisions.

## Conclusions

- This track should finish ontology-core rename coherently, including internal implementation names where they refer to current OM modeling concepts.
- Verification must include runtime tests plus textual scans for legacy API, table, column, DTO, and internal implementation names.

## T1.1 implementation note

- Updated ontology test/fixture expectations in `tests/Depa.Ontology.Tests/PublicSurfaceParityFixtures.cs`, `PermissionGovernanceParityFixtures.cs`, `SchemaEvolutionParityFixtures.cs`, `ExistentialGovernanceParityFixtures.cs`, `Program.cs`, and `OmScriptingJintContractTests.cs`.
- Test public/API terminology now expects `RelationDef`/`RelationLink`, `ComputedProp`, and `Operation` surfaces, including behavior catalog/import/runtime binding names and permission operation ABAC references. Mutation terminology remains unchanged.
- Added a public-surface ratchet in `PublicSurfaceParityFixtures.cs` requiring `DefineRelationDefAsync`, `CreateRelationLinkAsync`, `RetractRelationLinkAsync`, `DefineComputedPropAsync`, `DefineOperationAsync`, and `ExecuteOperationAsync`, and rejecting old public alias names such as `DefineRelationAsync`, `LinkEntitiesAsync`, `DefineComputedAsync`, `DefineActionAsync`, and `ExecuteActionAsync`.
- Scan command: `rg -n "ComputedPropsProp|UncreateRelationLink|CallParentAction|DefineActionCallback|ActionName|OmActionContext|BehaviorKind\.Action|BehaviorCatalogKind\.Action|DefineActionAsync|ExecuteActionAsync|RegisterAction|\.Actions\b|\baction\b|om_action_def|action_name|OmComputedContext|BehaviorKind\.Computed\b|BehaviorCatalogKind\.Computed\b|DefineComputedAsync|RegisterComputed\b|\.Computed\b|\bcomputed\b|om_computed_def|attr_name|DefineRelationAsync|DefineRelationAliasAsync|LinkEntitiesAsync|UnlinkEntitiesAsync|\bEdge\b|\bedge\b|om_edge|om_rel_def|om_rel_desc" tests/Depa.Ontology.Tests/*.cs` returned only the intentional old-name deny-list in `PublicSurfaceParityFixtures.cs`.
- Build command: `dotnet build tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore` is red as expected for this tests-first leaf: production still lacks `OmComputedPropContext` and `OmOperationContext`.

## T1.2 implementation note

- Updated direct Cozo fixture queries in `tests/Depa.Ontology.Tests/PublicSurfaceParityFixtures.cs` and `tests/Depa.Ontology.Tests/Program.cs` so behavior metadata owner storage now expects `class_name` instead of `type_name`.
- Updated computed/operation/interceptor direct storage probes to expect `om_computed_prop_def`/`computed_prop_name`, `om_operation_def`/`operation_name`, and `om_interceptor_def`/`operation_name` with `class_name` owner columns.
- Updated direct constraint metadata probes and overwrite fixtures to expect scalar discriminator column `constraint_kind` instead of `constraint_type`.
- Scan command: `rg -n "om_rel_def|om_rel_desc|om_edge|om_alias_rel|om_computed_def|om_action_def|om_perm_action|rel_name|from_type|to_type|from_id|to_id|\bprops\b|type_name|attr_name|action_name|constraint_type|owner_type|resource_type" tests/Depa.Ontology.Tests/*.cs` returned no matches.
- Build command: `dotnet build tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore` is red as expected for this tests-first leaf: production still lacks `OmComputedPropContext` and `OmOperationContext` (`OmScriptingJintContractTests.cs:2800` and `:2801`).

## T2.1 implementation note

- Renamed relation public API and DTO/storage surfaces in production: `CozoOm`, relation inputs/models, `RelationLogic`, relation portions of `TypeLogic`, `SchemaLogic`, `EntityLogic`, `ConstraintLogic`, `ExistentialRuleLogic`, runtime write context, scripting host bridge, query/analytics graph mapping, and batch ingestion now use `RelationDef`/`RelationLink` names and `om_relation_def`/`om_relation_desc`/`om_relation_link`/`om_alias_relation` with `relation_name`, `from_class`, `to_class`, `from_object_id`, `to_object_id`, and `payload`.
- Updated relation-link batch/example/test call sites in `src/Depa.Ontology/Om.Batch/*`, `examples/Depa.Ontology.ExampleServer/{DemoRunner.cs,DemoCatalog.cs,OmServerState.cs}`, and `tests/Depa.Ontology.Tests/Program.cs`; removed old public compatibility methods such as `DefineRelationAsync`, `LinkEntitiesAsync`, `UnlinkEntitiesAsync`, and `GetEdgeHistoryAsync`.
- Build command: `dotnet build tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore -v minimal` is red only for known out-of-scope T2.2/T2.3 names: missing `OmComputedPropContext` and `OmOperationContext` in `tests/Depa.Ontology.Tests/OmScriptingJintContractTests.cs:2800` and `:2801`. Production assemblies compiled.
- Example compile check: `dotnet build examples/Depa.Ontology.ExampleServer/Depa.Ontology.ExampleServer.csproj --no-restore -v minimal` succeeded with 0 warnings and 0 errors.
- Relation scan command: `rg -n "DefineRelationAsync|DefineRelationAliasAsync|LinkEntitiesAsync|UnlinkEntitiesAsync|GetEdgeHistoryAsync|EdgeHistoryEntry|OmBatchEdge|LinkEntitiesInput|DefineRelationInput|\bOmEdge\b|om_rel_def\b|om_rel_desc\b|om_edge\b|om_alias_rel\b|rel_name\b|from_type\b|to_type\b|from_id\b|to_id\b|\bprops\b" src/Depa.Ontology src/Depa.Ontology.Scripting.Jint tests/Depa.Ontology.Tests examples/Depa.Ontology.ExampleServer examples/Depa.Ontology.ExampleServer.Tests -g '*.cs' -g '*.md'` returns only the intentional old-name deny-list in `tests/Depa.Ontology.Tests/PublicSurfaceParityFixtures.cs`.
- Known remaining out-of-scope old names: analytics/query graph concepts still use generic graph `Edge`/`Edges` and their graph DTO fields such as `FromId`/`ToId`/`RelName`; existential rule spec fields still use `rel`/`toType`; computed/action/permission operation surfaces remain for T2.2/T2.3 except for minimal relation compile adjustments.

## T2.2 implementation note

- Renamed computed definition production surface to ComputedProp in `src/Depa.Ontology/Om.Core/CozoOm.cs`, `Inputs/OmInputs.cs`, `Runtime/CozoOmRuntime.cs`, `Logic/{ConstraintLogic,EntityLogic,BehaviorBindingLogic,BehaviorCatalogLogic,BehaviorImportLogic,BehaviorReadinessLogic,SchemaLogic}.cs`, `Contracts/Models/{BehaviorCatalogModels,BehaviorImportModels,BehaviorManifestModels}.cs`, and `src/Depa.Ontology.Scripting.Jint/JintBehaviorScriptProvider.cs`.
- Public/runtime surface now uses `DefineComputedPropAsync`, `RegisterComputedProp`, `TryGetComputedProp`, `OmComputedPropContext`, `BehaviorComputedPropCallbackBinding`, `BehaviorCallbackBindingSet.ComputedProps`, `BehaviorKind.ComputedProp`, `BehaviorCatalogKind.ComputedProp`, and manifest wire kind `computedProp`; no `DefineComputedAsync`/`RegisterComputed` compatibility aliases were added.
- Durable storage/query/schema now uses `om_computed_prop_def {class_name, computed_prop_name => description}` for computed property definitions while leaving durable `om_field_value`/FieldValue separate.
- Build command: `dotnet build tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore -v minimal` now compiles production assemblies and is red only for the out-of-scope T2.3 operation-context gap: `tests/Depa.Ontology.Tests/OmScriptingJintContractTests.cs:2801` missing `OmOperationContext`.
- Computed scan command: `rg -n "DefineComputedAsync|RegisterComputed\\b|TryGetComputed\\b|OmComputedContext|BehaviorKind\\.Computed\\b|BehaviorCatalogKind\\.Computed\\b|BehaviorComputedCallbackBinding|\\.Computed\\b|\\bComputed\\b|om_computed_def|attr_name" src/Depa.Ontology src/Depa.Ontology.Scripting.Jint tests/Depa.Ontology.Tests examples/Depa.Ontology.ExampleServer examples/Depa.Ontology.ExampleServer.Tests -g '*.cs' -g '*.md'` returns only the intentional old-name deny-list in `tests/Depa.Ontology.Tests/PublicSurfaceParityFixtures.cs`.
- Known remaining out-of-scope old names: operation/action and permission operation storage/API still remain for T2.3; constraint/mutation/interceptor owner columns still use `type_name` outside computed property definitions and are part of T2.3/T2.4 cleanup scope.

## T2.3 implementation note

- Renamed operation/action production surface in `src/Depa.Ontology/Om.Core/{CozoOm.cs,Inputs/OmInputs.cs,Runtime/CozoOmRuntime.cs}`, `Contracts/Models/{BehaviorCatalogModels,BehaviorImportModels,BehaviorManifestModels,OmModels}.cs`, `Logic/{BehaviorBindingLogic,BehaviorCatalogLogic,BehaviorImportLogic,BehaviorReadinessLogic,ConstraintLogic,SchemaLogic}.cs`, and `src/Depa.Ontology.Scripting.Jint/JintBehaviorScriptProvider.cs`.
- Public/runtime API now uses `DefineOperationAsync`, `ExecuteOperationAsync`, `RegisterOperation`, `TryGetOperation`, `OmOperationContext`, `OperationOwnerClass`, `CallParentOperationAsync`, `BehaviorOperationCallbackBinding`, `BehaviorKind.Operation`, `BehaviorCatalogKind.Operation`, manifest wire kind `operation`, and script host `callParentOperation`; no `DefineActionAsync`/`ExecuteActionAsync` aliases were added.
- Durable operation/governance storage now uses `om_operation_def {class_name, operation_name => description}`, `om_interceptor_def {class_name, operation_name, phase, seq => description}`, `om_perm_operation {operation => description}`, and `om_perm_policy {policy_id => effect, operation, resource_class, enabled, description}`.
- Permission API/model vocabulary now uses `CheckAccessInput.Operation`, `PermissionOperationSeed`, `PermissionSeedInput.Operations`, and policy `Operation`/`ResourceClass`.
- Updated operation-oriented tests and examples in `tests/Depa.Ontology.Tests/Program.cs`, `examples/Depa.Ontology.ExampleServer/{DemoCatalog.cs,DemoRunner.cs,OmServerState.cs}`, and `examples/Depa.Ontology.ExampleServer.Tests/Program.cs`; retained the generic delegate parameter `Func<CozoOm, Task<T>> action` as non-OM `System.Action`-style wording.
- Build command: `dotnet build tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore -v minimal` succeeded with 0 warnings and 0 errors.
- Example build command: `dotnet build examples/Depa.Ontology.ExampleServer/Depa.Ontology.ExampleServer.csproj --no-restore -v minimal` succeeded with 0 warnings and 0 errors.
- Test command: `dotnet run --project tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore` is red after compiling/running: it exits 134 at `tests/Depa.Ontology.Tests/Program.cs:3424` because the next cleanup slice still has production `om_constraint_def` using `type_name`/`constraint_type` while current tests probe `class_name`/`constraint_kind`.
- Operation scan command: `rg -n "DefineActionAsync|ExecuteActionAsync|RegisterAction\\b|TryGetAction\\b|TryGetActionRegistration|RestoreActionRegistration|UnregisterAction\\b|OmActionContext|CallParentAction|callParentAction|ActionOwnerType|BehaviorKind\\.Action|BehaviorCatalogKind\\.Action|BehaviorActionCallbackBinding|CallbackShape\\.Action|\\.Actions\\b|PermissionActionSeed|om_action_def|om_perm_action|action_name|\\\"action\\\"|\\baction\\b" src/Depa.Ontology src/Depa.Ontology.Scripting.Jint tests/Depa.Ontology.Tests examples/Depa.Ontology.ExampleServer examples/Depa.Ontology.ExampleServer.Tests -g '*.cs' -g '*.md'` returns only the intentional deny-list in `tests/Depa.Ontology.Tests/PublicSurfaceParityFixtures.cs` and the generic example helper delegate parameter `Func<CozoOm, Task<T>> action`.
- Known remaining out-of-scope old names: constraint owner/discriminator storage (`type_name`/`constraint_type`) and mutation owner storage still remain for T2.4; historical/public-surface deny-list strings remain intentional in `PublicSurfaceParityFixtures.cs`.

## T2.4 implementation note

- Removed current ontology-core internal Type/Entity/Attribute/Property implementation vocabulary for OM concepts. `TypeLogic.cs` and `EntityLogic.cs` were renamed to `ClassLogic.cs` and `ObjectLogic.cs`; internal DTOs/rows now use Class/Object/Field/FieldValue names such as `OmClassRow`, `OmObjectRow`, `OmFieldDefinition`, `OmFieldValueRecord`, `ObjectViewRow`, `FindByClassEntryRow`, and `ClassHierarchyRow`.
- Updated current constraint/mutation/behavior/interceptor owner storage and DTO/JSON naming from owner/type/constraint wording to Class/Kind wording where it stores OM class ownership: `om_constraint_def` now uses `class_name` and `constraint_kind`, mutation definitions use `class_name`, behavior binding rows use `owner_class`, behavior manifest/catalog/import/readiness DTOs and JSON use `ownerClass` and `constraintKind`, and permission policy/resource naming remains `operation`/`resource_class`.
- Cleaned FieldValue terminology in core object logic (`GetAllFieldValues*`, `WriteFieldValueAsync`, internal `FieldValues` row fields) while leaving generic JSON/reflection `property` loops and generic .NET `Type`/`Action` usages alone.
- Files changed in this leaf: `src/Depa.Ontology/Om.Core/{CozoOm.cs,Inputs/OmInputs.cs,Runtime/CozoOmRuntime.cs,Internals/OmConvert.cs,Contracts/Models/{OmModels,BehaviorCatalogModels,BehaviorImportModels,BehaviorManifestModels,BehaviorReadinessModels}.cs,Logic/{ClassLogic,ObjectLogic,BehaviorBindingLogic,BehaviorCatalogLogic,BehaviorImportLogic,BehaviorReadinessLogic,ConstraintLogic,ExistentialRuleLogic,RelationLogic,SchemaLogic}.cs}`, `src/Depa.Ontology.Scripting.Jint/JintBehaviorScriptProvider.cs`, `src/Depa.Ontology/Om.{Batch,Analytics,Query}/*`, and current tests/examples touched by the rename. Deleted old `Logic/TypeLogic.cs` and `Logic/EntityLogic.cs` in favor of `Logic/ClassLogic.cs` and `Logic/ObjectLogic.cs`.
- Build command: `dotnet build tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore -v minimal` succeeded with 0 warnings and 0 errors.
- Ontology test command: `dotnet run --project tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore` succeeded: `Depa.Ontology integration tests passed.`
- Example test command: `dotnet run --project examples/Depa.Ontology.ExampleServer.Tests/Depa.Ontology.ExampleServer.Tests.csproj --no-restore` succeeded: `Example server HTTP contract tests passed.`
- Internal/storage/API scan command: `rg -n "\bTypeLogic\b|\bEntityLogic\b|\bAttributeLogic\b|\bPropertyLogic\b|\bOmType\b|\bOmEntity\b|\bOmAttribute\b|\bOmProperty\b|DefineTypeInput|DefineAttributeInput|EntityInput|SetPropertyInput|FindByTypeOptions|CreateEntityAsync|UpsertEntityAsync|DeleteEntityAsync|GetEntityTypeAsync|SetPropertyAsync|GetPropertyAsync|ValidatePropertyTypeAsync|FindByTypeAsync|AggregateByTypeAsync|ValidateEntityAsync|FinalizeEntityAsync|ValidateRequiredPropertiesAsync|GetAllPropertiesAsync|GetAllPropertiesAsOfAsync|WritePropertyAsync|type_name|attr_name|constraint_type|owner_type|resource_type|ownerType|resourceType|constraintType|TypeName|AttrName|ConstraintType|OwnerType|ResourceType|ParentType|ToType|FromType|EntityId" src/Depa.Ontology src/Depa.Ontology.Scripting.Jint tests/Depa.Ontology.Tests examples/Depa.Ontology.ExampleServer examples/Depa.Ontology.ExampleServer.Tests -g '*.cs' -g '*.md'` returned no matches.
- Broader relation/computed/operation scan command returned only the intentional old public API deny-list in `tests/Depa.Ontology.Tests/PublicSurfaceParityFixtures.cs` and the generic helper delegate parameter `Func<CozoOm, Task<T>> action` in `examples/Depa.Ontology.ExampleServer/OmServerState.cs`.

## T3.1 implementation note

- Updated example demo contract in `examples/Depa.Ontology.ExampleServer/DemoCatalog.cs` and `DemoRunner.cs` so browser-visible tables now teach `Class`, `Field`, `RelationDef`, `ComputedProp`, `Operation`, `Object`, `FieldValue`, and `RelationLink` vocabulary with columns such as `className`, `fieldName`, `relationName`, `fromClass`, `toClass`, `computedPropName`, `operationName`, `fromObjectId`, `toObjectId`, and `payload`.
- Added concrete approval-flow `ComputedProp` and `Operation` definition rows and wired `SeedAsync` through `DefineComputedPropAsync`/`DefineOperationAsync`; graph responses now expose `relationLinks` in the example envelope.
- Tightened example HTTP contract assertions in `examples/Depa.Ontology.ExampleServer.Tests/Program.cs` for renamed table names/columns, approval-flow ComputedProp/Operation rows, relationLinks graph output, and permission `operation` requests.
- Updated `examples/Depa.Ontology.ExampleServer/README.md` to document the complete renamed example-browser contract and operation permission language.
- Example build command: `dotnet build examples/Depa.Ontology.ExampleServer/Depa.Ontology.ExampleServer.csproj --no-restore -v minimal` succeeded with 0 warnings and 0 errors.
- Example HTTP contract command: `dotnet run --project examples/Depa.Ontology.ExampleServer.Tests/Depa.Ontology.ExampleServer.Tests.csproj --no-restore` succeeded: `Example server HTTP contract tests passed.`
- Targeted example scan command: `rg -n "\b(Type|Entity|Attribute|Property|Edge|Action)\b|\b(type|entity|attribute|property|edge|action)\b|\bedges\b|om_rel_def|om_rel_desc|om_edge|om_alias_rel|om_computed_def|om_action_def|om_perm_action|rel_name|from_type|to_type|from_id|to_id|type_name|attr_name|action_name|owner_type|resource_type|constraint_type|props\b" examples/Depa.Ontology.ExampleServer examples/Depa.Ontology.ExampleServer.Tests -g '*.cs' -g '*.md' -g '*.json'` returned no matches.

## T4.2 parent recheck after gap fix

Time: 2026-08-14T22:38:02Z

- T4.2 initially found real remaining legacy terms in batch DTOs, Jint script host field-value helpers, relation shorthand parameters, and one interceptor `actionName` variable.
- Gap fix renamed:
  - `OmBatchEntity` / `OmBatchProperty` to `OmBatchObject` / `OmBatchFieldValue`;
  - batch `Entities` / `Properties` / `ValidatedEntities` to `Objects` / `FieldValues` / `ValidatedObjects`;
  - Jint host `getProperty` / `getPropertyAsOf` / `setProperty` to `getFieldValue` / `getFieldValueAsOf` / `setFieldValue`;
  - relation shorthand `relName` / `RelName` / `RelNames` / `OwnerRelNames` to `relationName` / `RelationName` / `RelationNames` / `OwnerRelationNames`;
  - analytics public `AnalyticsEdge` to `AnalyticsRelationLink`;
  - remaining OM operation variable `actionName` to `operationName`.
- Build command: `dotnet build tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore -v minimal` succeeded with 0 warnings and 0 errors.
- Ontology runtime command: `dotnet run --project tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore` succeeded with `Depa.Ontology integration tests passed.`
- Example runtime command: `dotnet run --project examples/Depa.Ontology.ExampleServer.Tests/Depa.Ontology.ExampleServer.Tests.csproj --no-restore` succeeded with `Example server HTTP contract tests passed.`
- Gap scan command: `rg -n "\b(OmBatchEntity|OmBatchProperty|getProperty\b|getPropertyAsOf\b|setProperty\b|relName\b|RelName\b|RelNames\b|OwnerRelNames\b|AnalyticsEdge\b|actionName\b|ActionName\b)" src tests examples -g '*.cs'` returned no matches.
- Full legacy API/storage scan command returned only the intentional old public API deny-list in `tests/Depa.Ontology.Tests/PublicSurfaceParityFixtures.cs`; those strings assert that old compatibility aliases are absent.
- The remaining `OmQueryGraph*.Properties` broad-scan hit is a generic JSON graph-property collection and is not the OM FieldValue/Property concept.

## T4.3 implementation report

Time: 2026-08-14T22:38:02Z

- Track `rename-ontology-operation-computedprop-contract` completed all phases.
- Ontology core now uses the mission vocabulary across the current implementation:
  - Class/Object/Field/FieldValue from the first slice;
  - RelationDef/RelationLink;
  - ComputedProp;
  - Operation and permission Operation;
  - Class/Kind owner/discriminator columns in current behavior, constraint, mutation, and governance storage.
- Removed old public compatibility aliases and current internal implementation names for OM Type/Entity/Attribute/Property/Edge/Action concepts.
- Final checks passed:
  - `dotnet build tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore -v minimal`
  - `dotnet run --project tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore`
  - `dotnet build examples/Depa.Ontology.ExampleServer/Depa.Ontology.ExampleServer.csproj --no-restore -v minimal`
  - `dotnet run --project examples/Depa.Ontology.ExampleServer.Tests/Depa.Ontology.ExampleServer.Tests.csproj --no-restore`
  - targeted legacy public/storage/internal vocabulary scans.

## T3.2 implementation note

- Updated current track behavior/knowledge guidance so the active delta case and knowledge context teach RelationDef/RelationLink/ComputedProp/Operation vocabulary instead of old action/edge/computed-def wording.
- Reviewed current durable Codument behavior/modeling docs in `codument/behaviors/dotnet-example-server.xml`, `codument/modeling/domain/example-server/index.xnl`, and `codument/modeling/architecture/example-server/index.xnl`, plus `examples/Depa.Ontology.ExampleServer/README.md`; they already use the renamed example-browser contract and did not need content changes in this leaf.
- Targeted current-doc scan command: `rg -n "\b(Type|Entity|Attribute|Property|Edge|Action|ComputedDef|RelDef|RelDesc|OmType|OmEntity|OmAttribute|OmProperty)\b|\b(type|entity|attribute|property|edge|action|computed def|computed definition|rel def|rel desc)\b|om_rel_def|om_rel_desc|om_edge|om_alias_rel|om_computed_def|om_action_def|om_perm_action|rel_name|from_type|to_type|from_id|to_id|type_name|attr_name|action_name|owner_type|resource_type|constraint_type|props\b|DefineRelationAsync|LinkEntitiesAsync|DefineComputedAsync|DefineActionAsync|ExecuteActionAsync" codument/behaviors codument/modeling examples/Depa.Ontology.ExampleServer/README.md codument/tracks/active/rename-ontology-operation-computedprop-contract/behavior_deltas codument/tracks/active/rename-ontology-operation-computedprop-contract/modeling_deltas codument/tracks/active/rename-ontology-operation-computedprop-contract/analysis -g '!**/archived/**'` now returns only intentional migration/evidence mentions in this active track's `analysis/findings.md`, `modeling_deltas/domain/ontology-om.xnl` `replaces` attributes, and regex false positives from current new wording such as `computed property`, `om_alias_relation`, and `computed props`.

## T4.1 final build/test verification

- `dotnet build tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore -v minimal` exited 0: build succeeded with 0 warnings and 0 errors.
- `dotnet run --project tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore` exited 0: `Depa.Ontology integration tests passed.`
- `dotnet build examples/Depa.Ontology.ExampleServer/Depa.Ontology.ExampleServer.csproj --no-restore -v minimal` exited 0: build succeeded with 0 warnings and 0 errors.
- `dotnet run --project examples/Depa.Ontology.ExampleServer.Tests/Depa.Ontology.ExampleServer.Tests.csproj --no-restore` exited 0: `Example server HTTP contract tests passed.`

## T4.2 final legacy-name scan

- Scope scanned: `src/Depa.Ontology`, `src/Depa.Ontology.Scripting.Jint`, `tests/Depa.Ontology.Tests`, `examples/Depa.Ontology.ExampleServer*`, `codument/behaviors`, `codument/modeling`, and this track's `behavior_deltas`, `modeling_deltas`, and `analysis` files. The track has `analysis/knowledge.md`; there is no separate `knowledge/` directory.
- Strict old public/API identifier scan command: `rg -n --pcre2 "\b(TypeLogic|EntityLogic|AttributeLogic|PropertyLogic|OmType|OmEntity|OmAttribute|OmProperty|DefineTypeInput|DefineAttributeInput|EntityInput|SetPropertyInput|FindByTypeOptions|CreateEntityAsync|UpsertEntityAsync|DeleteEntityAsync|GetEntityTypeAsync|SetPropertyAsync|GetPropertyAsync|ValidatePropertyTypeAsync|FindByTypeAsync|AggregateByTypeAsync|ValidateEntityAsync|FinalizeEntityAsync|ValidateRequiredPropertiesAsync|GetAllPropertiesAsync|GetAllPropertiesAsOfAsync|WritePropertyAsync|DefineRelationAsync|DefineRelationAliasAsync|LinkEntitiesAsync|UnlinkEntitiesAsync|GetEdgeHistoryAsync|EdgeHistoryEntry|OmBatchEdge|LinkEntitiesInput|DefineRelationInput|OmEdge|DefineComputedAsync|RegisterComputed|TryGetComputed|OmComputedContext|BehaviorComputedCallbackBinding|DefineActionAsync|ExecuteActionAsync|RegisterAction|TryGetAction|TryGetActionRegistration|RestoreActionRegistration|UnregisterAction|OmActionContext|CallParentAction|ActionOwnerType|BehaviorActionCallbackBinding|PermissionActionSeed)\b" ... -g '*.cs' -g '*.md' -g '*.xml' -g '*.xnl'` returned only allowed historical/evidence mentions in `analysis/findings.md` plus the intentional old-name public-surface deny-list in `tests/Depa.Ontology.Tests/PublicSurfaceParityFixtures.cs`.
- Strict old durable storage/column scan command: `rg -n --pcre2 "\b(om_type|om_entity|om_property|om_attr_def|om_attr_val|om_rel_def|om_rel_desc|om_edge|om_alias_rel|om_computed_def|om_action_def|om_perm_action|type_name|attr_name|owner_type|resource_type|constraint_type|action_name|rel_name|from_type|to_type|from_id|to_id)\b" src/Depa.Ontology src/Depa.Ontology.Scripting.Jint tests/Depa.Ontology.Tests examples/Depa.Ontology.ExampleServer examples/Depa.Ontology.ExampleServer.Tests codument/behaviors codument/modeling codument/tracks/active/rename-ontology-operation-computedprop-contract/behavior_deltas codument/tracks/active/rename-ontology-operation-computedprop-contract/modeling_deltas -g '*.cs' -g '*.md' -g '*.xml' -g '*.xnl'` exited 1 with no matches.
- Broad legacy shorthand scan command: `rg -n --pcre2 "\b(OmBatchEntity|OmBatchProperty|Entities|Properties|getProperty|getPropertyAsOf|setProperty|relName|actionName)\b" src/Depa.Ontology src/Depa.Ontology.Scripting.Jint tests/Depa.Ontology.Tests examples/Depa.Ontology.ExampleServer examples/Depa.Ontology.ExampleServer.Tests -g '*.cs'` found current source/test matches that are classified as failing for this rename verification:
  - `src/Depa.Ontology/Om.Batch/BatchInputs.cs` still exposes public batch vocabulary `OmBatchEntity`, `OmBatchProperty`, `Entities`, `Properties`, and `OmBatchResult.Entities/Properties`.
  - `src/Depa.Ontology.Scripting.Jint/JintBehaviorScriptProvider.cs` and `tests/Depa.Ontology.Tests/OmScriptingJintContractTests.cs` still expose/assert script host `getProperty`, `getPropertyAsOf`, and `setProperty` wording for OM field value access.
  - Current relation implementation still uses public/internal shorthand `relName` in `CozoOm`, runtime/query forwarding, `RelationLogic`, `ClassLogic`, analytics helpers, and a legacy schema migration alias fallback in `SchemaLogic`.
  - `src/Depa.Ontology/Om.Core/Logic/ConstraintLogic.cs` still uses `actionName` for interceptor operation lookup internals.
- Allowed broad-scan matches include old-name deny-list strings in `PublicSurfaceParityFixtures.cs`, historical evidence and scan commands in this `findings.md`, `modeling_deltas/domain/ontology-om.xnl` `replaces` attributes, generic JSON/reflection `.NET` `property`/`Type`/`Action` terms, and arbitrary test fixture data such as object ids or labels containing `entity`.
- Verification result: failing current public/internal legacy OM terminology remains; no code was changed in T4.2.

## T4.2 gap-fix note

- Fixed verifier gaps by renaming public batch DTOs/counts to `OmBatchObject`, `OmBatchFieldValue`, `OmBatchInput.Objects`, `OmBatchInput.FieldValues`, `OmBatchResult.Objects`, `OmBatchResult.FieldValues`, and `OmBatchResult.ValidatedObjects`; updated ingestion implementation and ontology tests accordingly.
- Renamed the Jint OM field-value host API and contract scripts from `getProperty`/`getPropertyAsOf`/`setProperty` to `getFieldValue`/`getFieldValueAsOf`/`setFieldValue`.
- Renamed current C# relation-name shorthand `relName` to `relationName` in public/runtime APIs, relation logic, analytics helpers, and schema-step input handling; removed the legacy `"relName"` schema-step alias fallback.
- Renamed the remaining interceptor lookup variable `actionName` to `operationName` in `ConstraintLogic`.
- Verification after fixes:
  - `dotnet build tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore -v minimal` exited 0.
  - `dotnet run --project tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore` exited 0: `Depa.Ontology integration tests passed.`
  - `dotnet run --project examples/Depa.Ontology.ExampleServer.Tests/Depa.Ontology.ExampleServer.Tests.csproj --no-restore` exited 0: `Example server HTTP contract tests passed.`
  - Targeted scans for batch `OmBatchEntity`/`OmBatchProperty`/`Entities`/`Properties`, Jint host `getProperty`/`getPropertyAsOf`/`setProperty`, relation shorthand `relName`, and `ConstraintLogic` `actionName` returned no matches in their scoped current files.
