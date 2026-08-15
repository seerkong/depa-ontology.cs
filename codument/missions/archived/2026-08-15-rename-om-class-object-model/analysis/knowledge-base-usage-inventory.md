# Knowledge-base dependent usage inventory

Task: G1-T2
Time: 2026-08-14T20:27:30Z

## Summary

The dependent knowledge-base project is tightly coupled to the current ontology OM vocabulary. The coupling appears in:

- C# ontology API calls;
- direct Cozo OM table queries;
- tests and parity fixtures copied from ontology behavior;
- root project skills and their ontology exchange specifications.

This confirms that the knowledge-base migration cannot be a small compile-only follow-up. It must update both executable code and long-lived skill assets.

## Ontology API usage

Scan scope: dependent project, excluding `bin`, `obj`, and `node_modules`.

| Legacy API/symbol | Occurrences |
| --- | ---: |
| `DefineTypeAsync` | 57 |
| `CreateEntityAsync` | 29 |
| `UpsertEntityAsync` | 11 |
| `DefineAttributeAsync` | 58 |
| `SetPropertyAsync` | 63 |
| `GetPropertyAsync` | 45 |
| `FindByTypeAsync` | 15 |
| `DefineActionAsync` | 9 |
| `ExecuteActionAsync` | 6 |
| `OmActionContext` | 6 |

Representative files:

- `src/Depa.KnowledgeBase.Depa/DepaOntologySchema.cs`
- `src/Depa.KnowledgeBase.Depa/DepaScanPipeline.cs`
- `src/Depa.KnowledgeBase.Depa/DepaViolationDetectors.cs`
- `tests/Depa.KnowledgeBase.IntegrationTests/Program.cs`
- `tests/Depa.KnowledgeBase.IntegrationTests/PublicSurfaceParityFixtures.cs`
- `tests/Depa.KnowledgeBase.IntegrationTests/SchemaEvolutionParityFixtures.cs`
- `tests/Depa.KnowledgeBase.IntegrationTests/ExistentialGovernanceParityFixtures.cs`
- `tests/Depa.KnowledgeBase.IntegrationTests/PermissionGovernanceParityFixtures.cs`
- `tests/Depa.KnowledgeBase.IntegrationTests/OmScriptingJintContractTests.cs`

## Direct OM table usage

Scan scope: dependent project, excluding `bin`, `obj`, and `node_modules`.

| Legacy table | Occurrences |
| --- | ---: |
| `om_type` | 7 |
| `om_mixin` | 1 |
| `om_type_mixin` | 3 |
| `om_attr_def` | 1 |
| `om_attr_desc` | 1 |
| `om_property` | 18 |
| `om_entity` | 10 |
| `om_rel_def` | 1 |
| `om_rel_desc` | 1 |
| `om_edge` | 8 |
| `om_action_def` | 2 |

Representative files:

- `src/Depa.KnowledgeBase.Depa/DepaReportQueries.cs`
- `src/Depa.KnowledgeBase.Depa/DepaViolationDetectors.cs`
- `tests/Depa.KnowledgeBase.IntegrationTests/Program.cs`
- `tests/Depa.KnowledgeBase.IntegrationTests/SchemaEvolutionParityFixtures.cs`
- `skills/ontology-exchange-xml-standard/ontology-domain/spec/cozo-om-projection.md`
- `skills/ontology-exchange-xml-standard/ontology-domain/spec/type-and-relation-resource.md`
- `skills/ontology-exchange-xml-standard/tests/validate-ontology-xml.test.ts`

## Legacy column and JSON-style names

Scan scope: dependent project, excluding `bin`, `obj`, and `node_modules`.

| Legacy name | Occurrences |
| --- | ---: |
| `type_name` | 16 |
| `parent_type` | 1 |
| `attr_name` | 18 |
| `entity_id` | 21 |
| `rel_name` | 9 |
| `from_id` | 69 |
| `to_id` | 66 |
| `action_name` | 3 |
| `typeName` | 32 |
| `parentType` | 21 |
| `attrName` | 5 |
| `entityId` | 37 |
| `relName` | 7 |
| `fromType` | 1 |
| `toType` | 2 |

## Skill asset signal

Root skill directories found:

- `skills/business-ontology-semantic-synthesis`
- `skills/code-knowledge-wiki-export`
- `skills/code-to-ontology-xml`
- `skills/depa-ontology-xml`
- `skills/investigate-business-knowledge-db`
- `skills/model-business-knowledge-cozo-om`
- `skills/ontology-exchange-xml-standard`

High-signal skill vocabulary counts under `skills/`, excluding `node_modules`:

| Term | Occurrences |
| --- | ---: |
| `ObjectType` | 204 |
| `BusinessObject` | 216 |
| `Property` | 131 |
| `Action` | 55 |
| `Type` | 80 |
| `Attribute` | 17 |
| `Edge` | 1 |
| `om_type` | 2 |
| `DefineTypeAsync` | 1 |

## Implementation implication

The knowledge-base track must update at least four strata:

1. C# code and tests that call renamed ontology APIs.
2. Direct Cozo queries that read/write renamed OM tables and columns.
3. Skill specifications, examples, tests, scripts, and prompt contracts that teach ontology XML or Cozo OM projection terminology.
4. Any versioned agent-skill copies if they repeat project-specific OM vocabulary rather than generic Codument workflow wording.
