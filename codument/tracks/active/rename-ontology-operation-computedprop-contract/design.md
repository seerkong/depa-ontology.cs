# Design: relation/computed/operation rename

## Frozen vocabulary

| Old name | New name |
| --- | --- |
| `om_rel_def` | `om_relation_def` |
| `om_rel_desc` | `om_relation_desc` |
| `om_edge` | `om_relation_link` |
| `om_alias_rel` | `om_alias_relation` |
| `om_computed_def` | `om_computed_prop_def` |
| `om_action_def` | `om_operation_def` |
| `om_perm_action` | `om_perm_operation` |
| `rel_name` | `relation_name` |
| `from_type` | `from_class` |
| `to_type` | `to_class` |
| `from_id` | `from_object_id` |
| `to_id` | `to_object_id` |
| `props` on relation links | `payload` |
| `type_name` in current OM owner contexts | `class_name` |
| `attr_name` in current OM field/computed contexts | `field_name` or `computed_prop_name` |
| `action_name` | `operation_name` |
| permission `action` | permission `operation` |

## API/model direction

- Relation APIs should speak `RelationDef` and `RelationLink`; method names should avoid `Edge`.
- Computed APIs and runtime callbacks should speak `ComputedProp`.
- Class-exposed upper-level actions should become Operations. Lower-level mutations and queries keep their established names.
- Permission seed/input/model names should use Operation where they model permissionable operations.
- The first slice's internal compatibility boundary (`TypeLogic`, `EntityLogic`, `OmType`, `OmEntity`, etc.) should be removed in this track because the second slice is the ontology-core completion slice.

## Storage direction

All current schema creation, schema snapshots, diff/rollback specs, and query logic should use only the new table and column names for active ontology-core storage. Legacy migrations from old external data are not part of this no-compatibility mission unless explicitly reapproved.

## Verification focus

- Build/test the ontology integration suite.
- Build/test the example server HTTP contract suite.
- Scan `src/Depa.Ontology`, `tests/Depa.Ontology.Tests`, `examples/Depa.Ontology.ExampleServer`, `examples/Depa.Ontology.ExampleServer.Tests`, and current non-archived Codument behavior/modeling docs for old storage/API/model names.
