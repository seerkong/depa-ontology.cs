# Ontology core legacy OM vocabulary inventory

Task: G1-T1
Time: 2026-08-14T20:26:08Z

## Summary

The legacy OM vocabulary is a whole-contract concern, not a local rename. It appears in:

- public C# APIs and model DTOs;
- Cozo durable schema relation names;
- Cozo query column names and JSON step fields;
- behavior, constraint, computed, interceptor, permission, and runtime registry surfaces;
- tests, fixtures, generated import/export scenarios, and example server code.

## Public API and model occurrences

Scan scope: `src`, `tests`, `examples`, `docs`.

| Legacy symbol | Occurrences |
| --- | ---: |
| `DefineTypeAsync` | 124 |
| `CreateEntityAsync` | 86 |
| `UpsertEntityAsync` | 5 |
| `DefineAttributeAsync` | 73 |
| `SetPropertyAsync` | 96 |
| `GetPropertyAsync` | 89 |
| `GetPropertyHistoryAsync` | 3 |
| `FindByTypeAsync` | 9 |
| `GetTypeHierarchyAsync` | 5 |
| `ResolveTypeAsync` | 43 |
| `ResolveAttributeAsync` | 3 |
| `DefineActionAsync` | 50 |
| `ExecuteActionAsync` | 37 |
| `RegisterAction` | 20 |
| `OmActionContext` | 38 |
| `OmAttribute` | 10 |
| `OmEntity` | 12 |
| `OmProperty` | 1 |
| `EntityView` | 7 |
| `TypeHierarchy` | 4 |

Representative files:

- `src/Depa.Ontology/Om.Core/CozoOm.cs`
- `src/Depa.Ontology/Om.Core/Contracts/Models/OmModels.cs`
- `src/Depa.Ontology/Om.Core/Logic/TypeLogic.cs`
- `src/Depa.Ontology/Om.Core/Logic/EntityLogic.cs`
- `src/Depa.Ontology/Om.Core/Logic/RelationLogic.cs`
- `src/Depa.Ontology/Om.Core/Logic/ConstraintLogic.cs`
- `src/Depa.Ontology/Om.Core/Logic/BehaviorImportLogic.cs`
- `src/Depa.Ontology/Om.Core/Runtime/CozoOmRuntime.cs`
- `src/Depa.Ontology.Scripting.Jint/JintBehaviorScriptProvider.cs`
- `src/Depa.Ontology/Om.Analytics/CozoOmAnalyticsExtensions.cs`
- `src/Depa.Ontology/Om.Batch/CozoOmBatchExtensions.cs`
- `examples/Depa.Ontology.ExampleServer/OmServerState.cs`
- `tests/Depa.Ontology.Tests/Program.cs`

## Durable table occurrences

Scan scope: `src`, `tests`, `examples`, `docs`.

| Legacy table | Occurrences |
| --- | ---: |
| `om_type` | 17 |
| `om_mixin` | 6 |
| `om_type_mixin` | 8 |
| `om_attr_def` | 10 |
| `om_attr_desc` | 6 |
| `om_property` | 27 |
| `om_computed_def` | 10 |
| `om_entity` | 17 |
| `om_rel_def` | 6 |
| `om_rel_desc` | 5 |
| `om_edge` | 19 |
| `om_alias_type` | 7 |
| `om_alias_attr` | 7 |
| `om_alias_rel` | 7 |
| `om_action_def` | 16 |
| `om_perm_action` | 6 |

Representative storage/query files:

- `src/Depa.Ontology/Om.Core/Logic/SchemaLogic.cs`
- `src/Depa.Ontology/Om.Core/Logic/TypeLogic.cs`
- `src/Depa.Ontology/Om.Core/Logic/EntityLogic.cs`
- `src/Depa.Ontology/Om.Core/Logic/RelationLogic.cs`
- `src/Depa.Ontology/Om.Core/Logic/ConstraintLogic.cs`
- `src/Depa.Ontology/Om.Core/Logic/BehaviorCatalogLogic.cs`
- `src/Depa.Ontology/Om.Core/Logic/BehaviorImportLogic.cs`
- `src/Depa.Ontology/Om.Core/Logic/ExistentialRuleLogic.cs`
- `tests/Depa.Ontology.Tests/SchemaEvolutionParityFixtures.cs`

## Column and JSON field occurrences

Scan scope: `src`, `tests`, `examples`, `docs`.

| Legacy name | Occurrences |
| --- | ---: |
| `type_name` | 213 |
| `parent_type` | 22 |
| `attr_name` | 91 |
| `entity_id` | 51 |
| `rel_name` | 51 |
| `from_type` | 8 |
| `to_type` | 8 |
| `from_id` | 29 |
| `to_id` | 32 |
| `value_type` | 12 |
| `constraint_type` | 21 |
| `resource_type` | 7 |
| `owner_type` | 16 |
| `action_name` | 51 |

## Implementation implication

The ontology-core rename should be split exactly as the mission planned:

1. a Class/Object/Field/FieldValue storage-contract track covering public APIs, models, table names, aliases, hierarchy, entity/object logic, and tests;
2. a RelationLink/ComputedProp/Operation track covering relation tables, computed/action/operation behavior, registry/interceptor/permission language, behavior import/export, scripting, and tests.

The scan also confirms that legacy Codument archived tracks and legacy specs contain historical terminology. Those historical records should not be bulk rewritten unless a later archive/documentation policy explicitly says so. The mission's no-legacy scan should distinguish current source/docs/tests/examples from historical Codument records.
