# Knowledge-base skills vocabulary inventory

Task: G1-T2B
Time: 2026-08-14T20:28:31Z

## Summary

The dependent project's root `skills/` directory must be included in the migration. The strongest hard coupling to the old OM API/table vocabulary is in `skills/ontology-exchange-xml-standard`, while several other skills use higher-level business ontology language that may or may not be renamed depending on the XML standard decision.

The mission should treat this as a skill-contract migration, not just documentation cleanup, because scripts and tests validate the current vocabulary.

## Skill roots

Root project skills:

- `skills/business-ontology-semantic-synthesis`
- `skills/code-knowledge-wiki-export`
- `skills/code-to-ontology-xml`
- `skills/depa-ontology-xml`
- `skills/investigate-business-knowledge-db`
- `skills/model-business-knowledge-cozo-om`
- `skills/ontology-exchange-xml-standard`

File count under `skills/` considered for this scan: 67 Markdown/TypeScript/XML/YAML files, excluding `node_modules`.

## Per-skill signal

`hard` means old ontology OM API/table/column terms such as `DefineTypeAsync`, `om_type`, `om_entity`, `om_property`, `type_name`, `attr_name`, or `action_name`.

`xml_terms` means ontology XML DSL terms such as `ObjectType`, `BusinessObject`, `Property`, `ComputedProperty`, `Action`, `TypeSystem`, `ScalarType`, or `EnumType`.

| Skill | hard | xml_terms | Migration note |
| --- | ---: | ---: | --- |
| `business-ontology-semantic-synthesis` | 0 | 1 | Mostly BO-level language; check prompt wording. |
| `code-knowledge-wiki-export` | 0 | 0 | Likely not directly affected. |
| `code-to-ontology-xml` | 0 | 1 | Check output contract names if XML DSL changes. |
| `depa-ontology-xml` | 0 | 4 | Check DEPA judgment / snapshot wording. |
| `investigate-business-knowledge-db` | 0 | 1 | Mostly investigation grouping language. |
| `model-business-knowledge-cozo-om` | 0 | 2 | Must follow target Cozo OM projection and generated-code API names. |
| `ontology-exchange-xml-standard` | 6 | 847 | Primary skill-contract migration target. |

## Hard OM references in skills

Current hard references are:

- `skills/ontology-exchange-xml-standard/ontology-domain/spec/cozo-om-projection.md`
  - maps `ObjectType` / `BusinessObject` to `defineType` and `om_type`;
  - mentions `om_type_mixin` and `(type_name, mixin_name)`.
- `skills/ontology-exchange-xml-standard/ontology-domain/spec/csharp-om-projection.md`
  - maps `ObjectType` to `DefineTypeAsync`;
  - maps `Property` to `DefineAttributeAsync`.
- `skills/ontology-exchange-xml-standard/tests/validate-ontology-xml.test.ts`
  - asserts snapshot keys `om_type` and `om_action_def`.

## DSL terminology that needs explicit design treatment

The XML standard currently has a large first-class language around:

- `ObjectType`
- `BusinessObject`
- `Property`
- `ComputedProperty`
- `Action`
- `TypeSystem`
- `ScalarType`
- `EnumType`
- `UnionType`
- `CollectionType`

These are not all automatically equivalent to the low-level Cozo OM vocabulary. The knowledge-base migration track must decide, with evidence, whether the XML DSL should:

1. keep `ObjectType` / `BusinessObject` as exchange-language concepts while only changing their projection to `Class` and `Field`;
2. rename exchange-language concepts to align more aggressively with `Class`, `Field`, `ComputedProp`, and `Operation`;
3. split low-level OM projection vocabulary from higher-level ontology XML terminology more explicitly.

The user requirement guarantees these skill assets must be reviewed and modified where needed; it does not require blind replacement of every occurrence.

## Implementation implication

The dependent knowledge-base track should include a dedicated skill asset task with acceptance criteria:

- update `cozo-om-projection.md` to the renamed OM tables/API;
- update `csharp-om-projection.md` to the renamed C# API;
- update validator tests/snapshots that assert old OM table or action names;
- audit SKILL.md prompt wording so agents no longer generate old OM API names;
- run skill validators/tests where available.
