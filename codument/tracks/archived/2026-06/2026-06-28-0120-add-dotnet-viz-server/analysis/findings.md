# Findings

## Found Facts
- `cozo-lib-bun-viz/server/src/index.js` exposes the backend API used by the Vue frontend:
  - `GET /health`
  - `GET /api/demos`
  - `POST /api/run`
  - `GET /api/permission/models`
  - `POST /api/permission/run`
  - `GET /api/schema/state`
  - `GET /api/schema/versions`
  - `POST /api/schema/diff`
  - `POST /api/schema/apply`
  - `POST /api/schema/rollback`
  - `GET /api/governance/seed-template`
  - `POST /api/governance/seed`
  - `POST /api/governance/checkAccess`
  - `POST /api/governance/explain`
  - `POST /api/governance/integrity/seed-demo`
  - `GET /api/governance/integrity/rules`
  - `POST /api/governance/integrity/check`
  - `POST /api/governance/integrity/apply`
- The frontend fetch helpers in `cozo-lib-bun-viz/frontend/src/lib/api.ts` and the governance/schema Vue pages consume the response shapes from those endpoints directly. A compatible .NET server must preserve JSON field names and top-level response envelopes.
- `cozo-lib-bun-viz/server/src/helpers.js` parses workbook/sheet rows into `{ entities, properties, edges }` batches and coerces typed values from strings. This is a reusable ingestion adapter, but not all of it is core OM: the workbook table names are viz/server conventions, while typed batch ingestion belongs in the OM library.
- `cozo-lib-bun-viz/server/src/index.js` calls several Node OM functions that are reusable library capabilities rather than HTTP-server concerns:
  - `om.ingestBatch`
  - `om.impactAnalysis`
  - `om.ownershipTree`
  - `om.riskHotspot`
  - `om.applySchemaMigration`
  - `om.rollbackSchema`
  - `om.seedPermissionMetadata`
  - `om.checkAccess`
  - existential rule list/check/apply
- The current C# OM already has partial coverage for schema state/snapshot/diff/rollback, permission metadata/checkAccess, and existential rules. It does not yet have full parity for:
  - typed batch ingestion with required validation summary
  - impact-analysis graph template
  - ownership-tree template
  - risk-hotspot ranking template
  - executable action/mutation/interceptor delegate pipeline
  - full schema migration apply semantics matching Node OM
  - permission seed shape matching `cozo-lib-bun-viz` governance seed tables
- `cozo-lib-bun-viz/server/src/permission/models.js` deliberately uses direct CozoScript over demo-specific relations, not the OM layer. This should remain in the viz-server package rather than be forced into `cozo-lib-dotnet`.
- Demo catalog modules under `cozo-lib-bun-viz/server/src/demos/*.js` combine seed tables, demo query definitions, and view conversion. The seed/query catalog is product/demo server content, not a bottom-layer OM feature, though the template functions it calls are OM features.

- User correction: `ingestBatch`, `impactAnalysis`, `ownershipTree`, and `riskHotspot` should not be added directly under the core ontology capsule. They should live in separate functional packages/capsules under `cozo-lib-dotnet/src/`, depending on the core ontology capsule.
- User correction: the original `cozo-lib-dotnet/src/Om/` core ontology capsule should be renamed to `cozo-lib-dotnet/src/Om.Core/`.

## Constraints
- Core ontology semantics remain in the renamed `cozo-lib-dotnet/src/Om.Core/`.
- `ingestBatch` should land in a separate `cozo-lib-dotnet/src/Om.Batch/` (or equivalent) package/capsule that depends on `Om.Core`.
- `impactAnalysis`, `ownershipTree`, and `riskHotspot` should land in a separate `cozo-lib-dotnet/src/Om.Analytics/` (or equivalent) package/capsule that depends on `Om.Core`.
- HTTP routes, demo catalog, workbook table conventions, response-shape adapters, and direct permission demo CozoScript should live in a new `cozo-lib-dotnet-viz-server` package.
- The new server must be compatible with the existing `cozo-lib-bun-viz/frontend` backend contract; the goal is to let the same frontend point at the .NET server.
- Keep the existing DEPA rule from `decision://dotnet-om-depa-capsule`: core OM logic should stay behind `ICozoOmStore`; server/web concerns should not leak into `cozo-lib-dotnet`.
- The previous user request mentioned committing changes, but the latest request explicitly superseded it with "先分析，然后用 codument-plan-track 创建track"; this track creation does not commit implementation changes.

## Open Questions
- Exact .NET project layout for `cozo-lib-dotnet-viz-server`: likely `cozo-lib-dotnet-viz-server/` with an ASP.NET Core minimal API project, but implementation should verify repo conventions before finalizing.
- Whether to port the demo catalog as C# static data/classes in the first implementation or load JSON fixtures generated from the Bun server modules. Static C# is more idiomatic for no Node dependency; generated JSON may speed parity.
- Whether `cozo-lib-dotnet-viz-server` should serve the existing frontend static assets or only expose API endpoints. The compatibility goal only requires backend endpoints; static serving can be optional.

## Conclusions
- Yes, `cozo-lib-bun-viz` contains reusable library capabilities that should be沉淀 into `cozo-lib-dotnet`, but not all directly into `src/Om.Core/`.
- Batch ingestion should become an `Om.Batch`-style functional capsule under `cozo-lib-dotnet/src/`.
- Graph/tree/ranking template queries should become an `Om.Analytics`-style functional capsule under `cozo-lib-dotnet/src/`.
- Executable behavior pipeline, full schema migration apply, and permission seed compatibility are closer to core/governance OM and can remain in or extend `src/Om/`.
- The .NET server package should implement the non-bottom-layer parts: HTTP API, demo catalog, workbook/table parsing conventions, response adapters, separate in-memory database lifetimes, and direct permission model query demos.
- Track id: `add-dotnet-viz-server`.

## Implementation Notes

### 2026-06-27 T1.0 Rename core Om capsule to Om.Core
- Moved the existing C# core OM source tree from `cozo-lib-dotnet/src/Om/` to `cozo-lib-dotnet/src/Om.Core/`.
- Kept the public namespace `Cozo.DotNet.Om` stable for source compatibility; documented the physical capsule path in `cozo-lib-dotnet/README.md`.
- `Cozo.DotNet.csproj` already compiles `src/**/*.cs`, so no explicit compile include change was required.
- Verification:
  - `dotnet test cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj -tl:off` exited 0.
  - `rg 'src/Om/|cozo-lib-dotnet/src/Om/|Include="src/Om' cozo-lib-dotnet codument/tracks/add-dotnet-viz-server` found no live code/project references to the old path; remaining matches are historical rename context in track artifacts.

### 2026-06-27 T1.1 Om.Batch ingestion capsule
- Added `cozo-lib-dotnet/src/Om.Batch/` as a separate functional capsule depending on the core `Cozo.DotNet.Om` facade.
- Added typed batch models and `CozoOmBatchExtensions.IngestBatchAsync`, with Node-compatible counts: entities, properties, edges, validatedEntities.
- Batch ingestion writes entities, properties, and edges, validates touched entities by default, and supports `ValidateRequired = false`.
- Workbook/table parsing remains outside `Om.Batch`; no server-specific Chinese sheet naming was added to the library capsule.
- Verification:
  - `dotnet run --project cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj -tl:off` exited 0 and covered successful batch writes, required validation failure, and skipped validation.
- Known gap:
  - Node `ingestBatch` uses `multiTransact(true)` and aborts on failure. Current .NET `ICozoOmStore` exposes only single `RunAsync`, so this first C# capsule does not yet provide rollback-on-failure parity. This should be revisited either by adding a transaction abstraction to `ICozoOmStore` or by using a single generated CozoScript transaction where practical.

### 2026-06-27 T1.2 Om.Analytics graph/tree/ranking capsule
- Added `cozo-lib-dotnet/src/Om.Analytics/` as a separate functional capsule depending on the core `Cozo.DotNet.Om` facade.
- Added typed template result models for graph, tree, and ranking visuals while keeping JSON-friendly field names through C# record properties.
- Implemented:
  - `ImpactAnalysisAsync` with BFS traversal, graph visual, legend by type, impacted count, max-depth/cycle/truncated stats.
  - `OwnershipTreeAsync` with tree visual and graph visual.
  - `RiskHotspotAsync` with base-score + degree-weight scoring, descending ranking, series values, evaluated/returned stats.
- No HTTP/demo/workbook concerns were added to the analytics capsule.
- Verification:
  - `dotnet build cozo-lib-dotnet/Cozo.DotNet.csproj -tl:off` exited 0.
  - `dotnet run --project cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj -tl:off` exited 0 and covered impact graph root flag/edge count, ownership tree children, and risk hotspot ordering/stats.

### 2026-06-27 T1.3 Executable behavior pipeline
- Extended the runtime-scoped `CozoOmRegistry` with mutation, action, and before/after interceptor delegates.
- Added action/mutation context helpers for property reads/writes, relation writes, and neighbor reads.
- Added facade overloads:
  - `DefineMutationAsync(..., executor, ...)`
  - `DefineActionAsync(..., handler, ...)`
  - `AddInterceptorAsync(..., handler, ...)`
  - `ExecuteActionAsync`
  - `ExecuteMutationsAsync`
- Metadata rows remain persisted through existing `om_action_def`, `om_mutation_def`, and `om_interceptor_def`; executable delegates remain process-local and are not treated as recoverable Cozo facts.
- Verification:
  - `dotnet run --project cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj -tl:off` exited 0 and covered before/action/mutation/after order plus property mutation.
  - `dotnet build cozo-lib-dotnet/Cozo.DotNet.csproj -tl:off` exited 0.
- Known gap:
  - Node `executeAction` uses `multiTransact(true)` and aborts all handler effects on failure. Current .NET action execution uses the existing sequential `ICozoOmStore.RunAsync` surface, so transaction rollback parity is not yet guaranteed.

### 2026-06-27 T1.4 Schema migration and permission seed parity
- Added `CozoOm.ApplySchemaMigrationAsync(SchemaMigrationSpec)` with migration validation, from-version snapshot materialization, step application, to-version snapshot/version/state update, and migration log write.
- Supported migration step kinds: `addType`, `addAttribute`, `addRelation`, `renameAttribute`, and `changeAttribute`.
- Added `PermissionSeedInput` plus action/policy/ABAC/path-rule seed records and `CozoOm.SeedPermissionMetadataAsync(PermissionSeedInput)`.
- Verification:
  - `dotnet run --project cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj -tl:off` exited 0 and covered schema apply with addType/addAttribute/addRelation plus permission seed for actions/policies/pathRules/abacRules.
  - `dotnet build cozo-lib-dotnet/Cozo.DotNet.csproj -tl:off` exited 0.
- P1 direction check:
  - PASS against coding attractor. Core ontology remains in `Om.Core`; reusable bottom-layer functions were split into `Om.Batch` and `Om.Analytics`; no HTTP/demo/workbook concerns entered `cozo-lib-dotnet`.

### 2026-06-27 T2.1 .NET viz server skeleton
- Added `cozo-lib-dotnet-viz-server/` with an ASP.NET Core minimal API project and `ProjectReference` to `cozo-lib-dotnet/Cozo.DotNet.csproj`.
- Added initial `/health`, `/api/demos`, and `/api/run` endpoints plus server README.
- Verification:
  - Started server with `dotnet run --project cozo-lib-dotnet-viz-server/Cozo.DotNet.VizServer.csproj --urls http://127.0.0.1:5099`.
  - `curl -sS http://127.0.0.1:5099/health` returned `{"status":"ok","runtime":".NET"}`.
  - `dotnet build cozo-lib-dotnet-viz-server/Cozo.DotNet.VizServer.csproj -tl:off` exited 0.
  - The first parallel build/run attempt failed due to ASP.NET obj cache file locking; rerunning build after stopping the server succeeded. This is a verification scheduling issue, not a code failure.

### 2026-06-27 T2.2 Demo catalog and /api/demos /api/run
- Added server DTOs, an initial HR demo catalog, and `/api/run` execution through the C# OM, `Om.Batch`, and `Om.Analytics` capsules.
- `/api/demos` returns frontend-compatible demo/query/table fields.
- `/api/run` supports:
  - table envelope for `employees`
  - graph envelope for `impactAnalysis`
  - tree envelope for `ownershipTree`
  - table/ranking envelope for `riskHotspot`
- Verification via a running server on `127.0.0.1:5099`:
  - `GET /api/demos` returned one demo with 4 queries and 3 tables.
  - `POST /api/run` for `employees` returned `{ status: "ok", table }` with 3 rows.
  - `POST /api/run` for `impactAnalysis` returned `{ status: "ok", graph }` with nodes/edges.
  - `POST /api/run` for `ownershipTree` returned `{ status: "ok", tree }` with root `emp:bob` and one child.
  - `POST /api/run` for `riskHotspot` returned `{ status: "ok", table }` with ranking columns and 3 rows.
- Known gap:
  - This is a first compatible C# demo, not a full migration of all six Bun demo catalogs.

### 2026-06-27 T2.3 Permission model demo endpoints
- Added `/api/permission/models` and `/api/permission/run` with a server-owned raw CozoScript RBAC demo.
- The permission demo seeds relations with `:replace` and runs direct CozoScript against a per-request in-memory `CozoDb`; it does not use `Om.Core`.
- Verification via a running server on `127.0.0.1:5099`:
  - `GET /api/permission/models` returned one model with one query and five tables.
  - `POST /api/permission/run` for `rbac-basic/userPermissions` returned `{ status: "ok", sections }` with one section and four rows.

### 2026-06-27 T2.4 Schema/governance/integrity endpoints
- Added server-scoped lifecycle state for schema, governance, and integrity demo databases.
- Added endpoints:
  - `/api/schema/state`, `/api/schema/versions`, `/api/schema/diff`, `/api/schema/apply`, `/api/schema/rollback`
  - `/api/governance/seed-template`, `/api/governance/seed`, `/api/governance/checkAccess`, `/api/governance/explain`
  - `/api/governance/integrity/seed-demo`, `/api/governance/integrity/rules`, `/api/governance/integrity/check`, `/api/governance/integrity/apply`
- Verification via running server on `127.0.0.1:5099`:
  - schema state returned currentVersion 1 and versions returned `[1]`
  - schema apply advanced currentVersion to 2; rollback returned currentVersion to 1
  - schema diff between v1 and v2 returned a non-empty diff
  - governance seed returned `{ ok: true }`; checkAccess returned allow=true for admin/read/proj1
  - integrity seed returned one rule; check returned two violations; apply created two objects and reached fixpoint
  - `dotnet build cozo-lib-dotnet-viz-server/Cozo.DotNet.VizServer.csproj -tl:off` exited 0
- P2 direction check:
  - PASS against coding attractor. HTTP routes, demo catalog, workbook-ish DTOs, raw permission demo CozoScript, and frontend response adapters are isolated in `cozo-lib-dotnet-viz-server`.

### 2026-06-27 T3.1 Endpoint contract tests
- Added `cozo-lib-dotnet-viz-server/tests/` as a lightweight console contract test runner that targets a running server via `VIZ_SERVER_BASE_URL`.
- Covered health, demos/run table/graph/tree/ranking, permission model/run, schema state/apply/rollback, governance seed/checkAccess, and integrity seed/rules/check/apply.
- Updated server project to exclude `tests/**/*.cs` from the web app compilation.
- Verification:
  - `dotnet build cozo-lib-dotnet-viz-server/Cozo.DotNet.VizServer.csproj -tl:off` exited 0.
  - `dotnet build cozo-lib-dotnet-viz-server/tests/Cozo.DotNet.VizServer.Tests.csproj -tl:off` exited 0.
  - With server running on `127.0.0.1:5099`, `VIZ_SERVER_BASE_URL=http://127.0.0.1:5099 dotnet run --project cozo-lib-dotnet-viz-server/tests/Cozo.DotNet.VizServer.Tests.csproj -tl:off` exited 0.

### 2026-06-27 T3.2 Docs and run instructions
- Updated `cozo-lib-dotnet-viz-server/README.md` with:
  - server start command
  - `VITE_API_BASE=http://127.0.0.1:5099` frontend example
  - supported endpoint list
  - `cozo-lib-dotnet` vs viz-server boundary
  - contract test command

### 2026-06-27 T3.3 Final verification
- `dotnet run --project cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj -tl:off` exited 0.
- `dotnet build cozo-lib-dotnet-viz-server/Cozo.DotNet.VizServer.csproj -tl:off` exited 0.
- `VIZ_SERVER_BASE_URL=http://127.0.0.1:5099 dotnet run --project cozo-lib-dotnet-viz-server/tests/Cozo.DotNet.VizServer.Tests.csproj -tl:off` exited 0.
- `codument validate add-dotnet-viz-server --strict` exited 0.
- Boundary scan for `DemoCatalog`, `PermissionModels`, HTTP route mapping, `VITE_API_BASE`, and workbook/table-specific terms found these server-only concerns only in `cozo-lib-dotnet-viz-server`, not under `cozo-lib-dotnet/src`.

### 2026-06-27 P3 Gap-loop
- Initial gap found: `/api/governance/seed-template` was returning the HR demo tables rather than governance-oriented seed tables.
- Fix applied: replaced seed-template output with governance seed tables covering types, entities, permission actions, policies, ABAC rules, and path rules.
- Re-verification:
  - `dotnet build cozo-lib-dotnet-viz-server/Cozo.DotNet.VizServer.csproj -tl:off` exited 0.
  - `VIZ_SERVER_BASE_URL=http://127.0.0.1:5099 dotnet run --project cozo-lib-dotnet-viz-server/tests/Cozo.DotNet.VizServer.Tests.csproj -tl:off` exited 0.
- Remaining non-blocking gaps:
  - C# batch/action execution does not yet provide Node `multiTransact(true)` rollback parity because `ICozoOmStore` has no transaction abstraction.
  - The .NET demo catalog currently provides a compatible HR demo plus full endpoint coverage, but does not yet port all six Bun demo catalogs.

### 2026-06-27 Track revision for transaction and full demo parity
- User promoted the two remaining gaps into explicit current-track tasks:
  - `T1.5` adds `ICozoOmStore` transaction abstraction and implementation.
  - `T2.5` migrates all six Bun demo catalogs.
- Transaction source reference:
  - `cozo-lib-bun/index.js` exposes `CozoDb.multiTransact(write)` returning a transaction runner with query/commit/abort.
  - `cozo-lib-bun/cozo-om.js` requires `multiTransact(true)` for `ingestBatch`, `executeAction`, and `executeMutations`.
  - `cozo-lib-nodejs/src/lib.rs` implements this through `DbInstance::multi_transaction(write)`.
- Design decision:
  - Real rollback parity requires extending `cozo-lib-c` with transaction begin/run/commit/abort functions, then wrapping them in `cozo-lib-dotnet`.
  - A C#-only `ICozoOmStore` abstraction would be useful shape but would not rollback already executed Cozo queries.
- Demo parity target:
  - `/api/demos` must include the six Bun ids: `procurement`, `hr`, `crm`, `it-asset`, `approval-flow`, `org-timeline`.
  - `/api/run` should cover table/tree/graph/ranking/action/temporal-style demo outputs with the existing frontend envelope.

### 2026-06-27 T1.5 Transaction abstraction implementation
- Added C ABI functions in `cozo-lib-c`:
  - `cozo_multi_transact`
  - `cozo_run_tx`
  - `cozo_commit_tx`
  - `cozo_abort_tx`
- Added .NET wrapper support:
  - `CozoDb.BeginTransaction`
  - `CozoTransaction`
  - P/Invoke bindings in `CozoNative`
- Added OM transaction abstraction:
  - `ICozoOmStore.BeginTransactionAsync`
  - `ICozoOmTransaction`
  - `CozoDbOmStore` + transaction store implementation
- Updated write pipelines:
  - `Om.Batch.IngestBatchAsync` writes/validates inside one transaction and commits only after validation.
  - `ExecuteActionAsync` and `ExecuteMutationsAsync` run against transaction-backed runtime stores.
- Rollback tests added:
  - failed batch required validation does not leave `bp_missing`.
  - failing after interceptor does not persist a prior mutation to `p1.name`.
- Verification:
  - `./cozo-lib-dotnet/build-native.sh osx-arm64` exited 0 and copied `libcozo_c.dylib` to `cozo-lib-dotnet/runtimes/osx-arm64/native/`.
  - `dotnet run --project cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj -tl:off` exited 0.

### 2026-06-27 T2.5 Six Bun demo catalog migration
- Replaced the initial HR-only .NET demo catalog with six demo definitions:
  - `procurement`
  - `hr`
  - `crm`
  - `it-asset`
  - `approval-flow`
  - `org-timeline`
- The .NET server now stores demo seed schema and batch seed data as server-local C# definitions.
- `/api/run` now dispatches demo query plans for:
  - table-style DSL parity queries
  - impact graph queries
  - ownership tree queries
  - risk hotspot ranking/table queries
  - approval action-style queries
  - org timeline temporal-style table queries
- Contract tests now assert all six demo ids are listed and run each demo's first query, while retaining HR table/graph/tree/ranking coverage.
- Verification:
  - `dotnet build cozo-lib-dotnet-viz-server/Cozo.DotNet.VizServer.csproj -tl:off` exited 0.
  - With server on `127.0.0.1:5099`, `VIZ_SERVER_BASE_URL=http://127.0.0.1:5099 dotnet run --project cozo-lib-dotnet-viz-server/tests/Cozo.DotNet.VizServer.Tests.csproj -tl:off` exited 0.
