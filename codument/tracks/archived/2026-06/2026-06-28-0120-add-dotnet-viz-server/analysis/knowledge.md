# Knowledge Context

## Source Notes
| Source | Summary | Relevance |
|--------|---------|-----------|
| `cozo-lib-bun-viz/server/src/index.js` | Defines Elysia routes, demo execution orchestration, schema/governance/integrity APIs, and demo database lifetimes. | Primary backend contract to reproduce in ASP.NET Core. |
| `cozo-lib-bun-viz/frontend/src/lib/api.ts` | Defines frontend request/response TypeScript types and fetch helpers. | JSON compatibility target for .NET server. |
| `cozo-lib-bun-viz/server/src/helpers.js` | Parses workbook sheets into OM batch input and coerces string values by schema type. | Split into server-specific table parser plus OM-level batch ingestion. |
| `cozo-lib-bun-viz/server/src/demos/*.js` | Demo schema/data/query catalogs; some queries call OM template functions. | Server package owns catalog; OM package owns reusable template functions. |
| `cozo-lib-bun-viz/server/src/permission/models.js` | Direct CozoScript permission model demos using `:replace` and raw query execution. | Should remain viz-server demo code, not OM core. |
| `cozo-lib-bun/cozo-om.d.ts` | Public Node OM API includes `ingestBatch`, `impactAnalysis`, `ownershipTree`, `riskHotspot`, action/mutation/interceptor execution, schema migration, permission, existential rules. | Parity checklist for `cozo-lib-dotnet`. |
| `cozo-lib-dotnet/src/Om.Core/` | Renamed DEPA C# core OM capsule from prior `cozo-lib-dotnet/src/Om/`. | Core ontology dependency for new functional capsules. |

## Codebase Knowledge
- The Bun viz server has three database lifetimes:
  - per-request fresh DB for `/api/run` and `/api/permission/run`;
  - shared `sharedDb` for schema versioning endpoints;
  - resettable `governanceDb` and `integrityDb` for governance demos.
- The server serializes OM registry-sensitive operations with `withOmRegistryLock` because Node OM keeps JS handler registries at module scope. C# should avoid module-global registry races by keeping behavior registries inside `CozoOmRuntime` or server-scoped `CozoOm` instances.
- `/api/run` transforms Node OM template results into frontend-specific shapes:
  - graph template result -> `{ status: "ok", graph: { nodes, edges } }`
  - tree template result -> `{ status: "ok", tree: TreeNode[] }`
  - ranking template result -> `{ status: "ok", table: { columns, rows } }`
- Governance integrity endpoints are thin wrappers over OM existential rules plus deterministic demo seeding.
- Schema endpoints are thin wrappers over OM schema state/version/diff/migration/rollback.

## Domain Knowledge
- "底层能力" means reusable library semantics that should be available without the viz server. Per user correction, not every such ability belongs directly in `src/Om.Core/`: batch ingestion and analytics templates should be separate functional capsules under `cozo-lib-dotnet/src/` that depend on `Om.Core`.
- "非底层能力" means transport and demo presentation: HTTP routes, frontend-specific response envelopes, demo catalog, workbook table naming, direct raw CozoScript permission demos.

## Terms
| Term | Meaning |
|------|---------|
| viz-server | The new ASP.NET Core server package compatible with the existing Bun viz backend API. |
| workbook table convention | Chinese-named table groups such as `类型定义`, `属性定义`, `实体数据`; server input format, not core OM. |
| template query | Reusable OM analysis helpers such as impact analysis, ownership tree, and risk hotspot. |
| behavior pipeline | In-memory action/mutation/interceptor delegates plus persisted metadata rows. |
| Om.Core | Renamed core ontology capsule formerly planned as `cozo-lib-dotnet/src/Om/`. |
| Om.Batch | Proposed C# functional capsule for typed batch ingestion based on `Om.Core`. |
| Om.Analytics | Proposed C# functional capsule for graph/tree/ranking analysis based on `Om.Core`. |
