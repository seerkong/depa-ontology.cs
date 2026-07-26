# Knowledge Context

## Source Notes

| Source | Summary | Relevance |
| --- | --- | --- |
| Active mission design | Defines G15 registry lifecycle, G16 portability/readiness, and G17 behavior schema evolution. | Final acceptance contract. |
| `unify-om-runtime-registry-lifecycle` | Establishes runtime-instance callback ownership and clear/reset semantics. | G15 evidence source. |
| `add-bun-om-behavior-portability` | Establishes C# V1 catalog/manifest/binding/readiness contract in Bun. | G16 evidence source. |
| `extend-bun-om-behavior-schema-versioning` | Establishes Bun behavior snapshot/diff/migration/rollback and D8 policy. | G17 evidence source. |

## Codebase Knowledge

- Runtime callback registries are process-local while definitions and binding
  identities are durable Cozo relations.
- Catalog readiness is derived from the persisted binding identity and the
  current runtime registry; restoring schema must not restore executable code.
- Bun legacy free functions are compatibility adapters over an explicit legacy
  runtime; .NET uses one registry per `CozoOm` instance.

## Terms

| Term | Meaning |
| --- | --- |
| G15 | Runtime-instance callback registry lifecycle parity. |
| G16 | Canonical behavior manifest, binding identity, and readiness parity. |
| G17 | Bun behavior-aware schema snapshot, diff, migration, and rollback parity. |
| D8 | Legacy snapshot compatibility policy: reject by default, explicit preserve or clear. |
