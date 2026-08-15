# Mission proposal: Rename OM modeling language to Class/Object/Field/Operation

Status: completed

This mission captures the agreed breaking redesign of the ontology object-model language. The existing system uses a Type/Entity/Attribute/Property/Edge/Action vocabulary inherited from earlier Cozo OM work. That language is now too broad and sometimes conflicts with C# and ORM conventions, so the target is a clean domain vocabulary with no compatibility layer.

## Target vocabulary

- Class: object category definition.
- Object: concrete persisted object data.
- Instance: relationship/documentation term only; an object may be described as an instance of a class, but Instance should not become a primary API/table concept.
- Field: Class-defined storable field.
- FieldValue: Object-held durable value for a Field.
- ComputedProp: Class-defined computed property/getter-like derived member.
- RelationDef: relation type definition.
- RelationLink: explicit persisted relation link between objects.
- Operation: Class-exposed upper-level operation; it may be composed from lower-level Mutation and Query building blocks.
- Mutation: lower-level write behavior.
- Query: lower-level read behavior.

## Scope

The mission covers:

1. Freezing the glossary and rename matrix.
2. Creating implementation tracks for the ontology core project.
3. Updating schema tables, Cozo queries, public API, model names, docs, tests, migrations, snapshots, scripts, and examples.
4. Creating and executing a follow-up track for the dependent knowledge-base project.
5. Updating the dependent project's skill assets, including business skills, references, prompt contracts, scripts, tests, and any versioned agent-skill copies that repeat the OM vocabulary.
6. Verifying both projects against the new vocabulary.
7. Promoting durable Codument decisions before archiving the mission.

## Non-goals

- No old Type/Entity/Attribute/Property/Edge/Action compatibility aliases.
- No adapters that keep old APIs alive.
- No legacy schema read/write support for old table names.
- No broad rewrite of unrelated programming-language uses of words like type, class, property, object, or action unless they are part of the OM contract.
- No semantic split of inferred relation results beyond reserving RelationLink for explicit persisted links. If derived relation inference needs separate modeling later, it should become a separate mission/track.

## Success criteria

- The ontology core public OM API exposes the new Class/Object/Field/FieldValue/ComputedProp/Relation/Operation language.
- Durable OM storage table names and relevant columns use the new vocabulary.
- Tests and docs describe the new vocabulary and do not teach legacy terms.
- The dependent knowledge-base project compiles/tests against the renamed ontology contract.
- The dependent knowledge-base project's skills and skill-adjacent assets no longer teach or validate the old OM vocabulary.
- Scans show no old OM-specific public/storage vocabulary remains except historical notes or third-party language constructs that are intentionally out of scope.

## Implementation policy

This mission is a control plan. Actual code changes should be done through child tracks linked from `mission.xml`. Because the redesign is intentionally breaking, each implementation track should prefer direct replacement over compatibility shims.
