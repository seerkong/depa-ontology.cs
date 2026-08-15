# Proposal: rename ontology operation/computed/relation contract

## Goal

Complete the ontology-core side of the mission-approved breaking rename after the Class/Object/Field/FieldValue slice.

This track renames the remaining low-level OM vocabulary:

- `RelationDef` / `RelationLink` replace relation definition and persisted edge wording.
- `ComputedProp` replaces computed definition / computed property ambiguity.
- `Operation` replaces action as the class-exposed upper-level operation concept.
- Permission action vocabulary follows the operation rename.
- Current ontology-core implementation internals should stop carrying Type/Entity/Attribute/Property names once this slice is complete.

## Non-goals

- Do not migrate `/Users/kongweixian/infra-dev/ontology/depa-knowledge-base.cs`; that is mission G3.
- Do not provide compatibility aliases for old public APIs or old table names.
- Do not rewrite historical archived Codument records.

## Acceptance

- Public APIs, DTOs, storage tables/columns, tests, example server, and current behavior docs use the new terms for this slice.
- `om_rel_def`, `om_rel_desc`, `om_edge`, `om_computed_def`, `om_action_def`, and `om_perm_action` are replaced in current durable schema and direct queries.
- `action` concepts that represent class-exposed operations are renamed to `operation`, including permission operation naming.
- Remaining Type/Entity/Attribute/Property internal implementation names from the first slice are removed or renamed where they refer to current OM modeling concepts.
- Ontology and example test suites pass.
