# P1 Attractor Check 001

## Verdict

`PASS`

No blocking finding was identified. P1 establishes a complete, executable
G01-G14 evidence contract and remains aligned with the project/product
attractors, the track proposal/design/behavior delta, and the mission's frozen
D0-D5, D3a, and D3b decisions.

This verdict does not claim fresh runtime completion. P2 still owns the Bun
full suite, .NET build/full and focused runs, optional-package contracts, and
Codument strict validation.

## Findings

### No unresolved G01-G14 evidence gap

The T1.1 inventory has exactly one substantive row for each G01-G14 item. Every
row records the desired behavior, current Bun and .NET state, executable test
entry, governing decision or intentional difference, linked completed track,
final verification evidence, and current verdict.

The seven linked implementation tracks resolve to `completed`. Their cited
closure reports record `PASS` or `NO_GAP`; no inventory row depends only on a
track status.

### Resolved: four Bun cases needed direct executable evidence

T1.2 correctly challenged the initial `COVERED` inventory and repaired four
test-only omissions:

- G12 now calls `listExistentialRules` alongside check/apply before schema
  initialization and requires a safe empty result.
- G13 now rejects an unknown owner, unsupported constraint scope, and invalid
  interceptor phase; verifies zero persisted metadata; and proves the rejected
  callback cannot leak into later action execution.
- G14 now distinguishes omitted parent preservation from explicit
  `parentType: null` clearing.
- G14 now directly exercises the exported `inferValueType` contract, including
  unknown/non-finite values.

The changes are confined to four Bun test files. They do not modify production
runtime behavior, public signatures, track state, mission state, or unrelated
packages. Scoped `git diff --check` passes.

### Intentional differences are decision-backed

All non-identical cross-language behavior in the matrix is justified by an
explicit user decision:

| Scope | Governing decision | Accepted difference |
| --- | --- | --- |
| G01-G02, G12-G14 | D0, D4, D5 | Observable parity with typed async/cancellation APIs and explicit `Keep/Set/Clear`; legacy nullable overload remains a migration wrapper. |
| G03-G05 | D3, D3a, D3b, D4 | C# adds portable catalog/readiness, canonical JSON plus YAML adapter, and isolated Jint because Bun callbacks are native JavaScript. |
| G06-G07 | D1 | Permission behavior converges on strict directed witnesses and fail-closed temporal ABAC; no legacy authorization bypass is retained. |
| G08-G11 | D2, D4 | Strict atomic guarantees are additive V2 contracts; legacy wrappers remain temporarily, .NET initialization is detect-only by default, and broader C# snapshots are preserved. |

No evidence contradicts these decisions or requires reopening the decision
frontier.

### Test entries are real

- Bun declares `"test": "bun test"` and discovers the repaired files under
  `cozo-lib-bun/__tests__`.
- The .NET full harness directly invokes the core behavior, permission, and
  optional Jint contracts.
- `COZO_OM_TEST_FOCUS` exposes `permission-governance`,
  `schema-evolution`, `existential-governance`, and `public-surface`.
  Schema, existential, and public-surface parity therefore require explicit
  focused P2 runs in addition to the full harness.
- T1.2's focused Bun command completed with `31 pass`, `0 fail`, and
  `102 expect()` calls across the four repaired files.

## G01-G14 Completeness

| Items | P1 result | Evidence character |
| --- | --- | --- |
| G01-G02 | Complete | Paired inheritance, parent dispatch, ordering, rollback, and transaction cases. |
| G03-G05 | Complete | Bun native behavior evidence plus decision-backed C# portability, manifest, YAML, and Jint contracts. |
| G06-G07 | Complete | Paired strict permission-witness and temporal ABAC fixtures. |
| G08-G11 | Complete | Paired strict V2 rollback/migration, legacy handling, atomicity, and keyed-diff fixtures. |
| G12 | Complete | Paired operator, alias/version, historical storage, and no-init list/check/apply cases. |
| G13 | Complete | Paired fail-before-effect and no-ghost behavior registration cases. |
| G14 | Complete | Paired parent clear, constraint facade, traversal, and direct value-inference cases. |

## Attractor Alignment

The project attractor calls for stable multi-language bindings around the same
CozoDB graph/ontology kernel, and the product attractor identifies test
stability across primary bindings as a success criterion. P1 supports both by
comparing observable semantics while preserving approved language-specific
architecture.

The work also follows the track boundary: it verifies OM parity only, keeps
production changes conditional on concrete evidence, and does not touch Wiki,
Java indexing, visualization, analytics, batch, or archival state. No product
scope expansion or engineering-boundary erosion was found.

## P2 Preconditions

P2 may start. It must:

1. Run the Bun full suite and the focused behavior, governance, schema,
   existential, and public-surface files, recording commands and counts.
2. Build the .NET OM test project; run the full harness; run all four focus
   values; and confirm portability, YAML, and Jint contracts execute.
3. Resolve every mission-bound TrackLink and run XML plus strict Codument
   validation.
4. Treat any fresh runtime contradiction in security, migration defaults,
   serialization, scripting, or public compatibility as a decision blocker,
   not as a silent repair.

## Residual Risk

The shared working tree contains concurrent changes, and T1.2 intentionally did
not run either runtime's complete verification surface. Static evidence and the
focused Bun repair run cannot exclude integration, build, native-library, or
cross-package regressions. Those risks remain explicitly gated by P2 and do not
invalidate the P1 evidence contract.
