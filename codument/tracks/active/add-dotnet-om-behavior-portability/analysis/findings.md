# Findings

## Baseline

- Persistent behavior definitions and executable callbacks currently live in separate C# OM state surfaces.
- G3 made definition writes and callback registration compensatable but did not add serialization or readiness projection.
- The mission decisions require unresolved import to be honest and fail closed rather than pretending callbacks are portable.

## Implementation Questions To Resolve In P1

- Binding identity is persisted in an additive relation; executable readiness is derived from the persisted key plus an immutable typed-registry snapshot.
- Strict import uses effect-free preflight, staged registry state, a persistent write transaction, a runtime publication gate, and explicit compensation.
- YAML is isolated in `packages/Om.Portability.Yaml` with YamlDotNet 18.1.0 and bounded AST conversion; Om.Core remains dependency-free.
- Failure-first tasks cover process restart, every behavior entry point, concurrent visibility, persistent/publish/compensation failures, and sparse interceptor keys.

## T1.0 Spot Check

- Added the exact persisted binding key relation and internal validated put/list/query helpers without serializing delegates or adding product public API.
- Schema init, current schema object, checksum snapshots, rollback and legacy additive initialization include the new relation.
- Tests cover all five behavior kinds, constraint when/then slots, exact interceptor phase/seq, invalid keys, snapshot rollback, and SQLite close/reopen. Reopened bindings remain persisted while a fresh registry has no executable callbacks.
- Parent verification: build completed with 0 warnings and 0 errors; the complete OM harness and `git diff --check` passed.

## T1.1 Spot Check

- Added public immutable catalog records and `CozoOm.GetBehaviorCatalogAsync`; the records contain only serializable metadata, callback identity and readiness, never delegates.
- Catalog projection joins all five behavior definition relations with persisted bindings and the current typed registry. It distinguishes `Unbound`, `Unresolved` and `Ready`, including constraint `when`/`then`/`validator` and exact interceptor `(owner, action, phase, seq)` keys.
- SQLite close/reopen tests prove binding ids survive while a fresh registry projects unresolved; typed re-registration returns the same bindings to ready.
- Interceptor extension now allocates owner/action/phase-local `max(seq)+1`, preserving sparse imported keys.
- Parent verification at 2026-07-16T20:53:23Z: `git diff --check`, `dotnet build tests/Cozo.DotNet.Om.Tests.csproj --no-restore` (0 warnings/errors), and the complete OM harness all passed.

## T1.2 Spot Check

- Added a version 1 canonical JSON codec whose normalization makes behavior and callback input ordering irrelevant to the emitted UTF-8 bytes.
- Decode is effect-free and returns immutable structured diagnostics for malformed JSON, unsupported version, unknown kind/slot/readiness, invalid owner/key/slot combinations, duplicate callback bindings and duplicate/conflicting behavior keys.
- Five behavior kinds, all callback slots, reversed-order normalization, cancellation and decode/re-encode byte stability are covered in the OM harness; P2 import effects, YAML and scripts remain outside this task.
- Parent verification at 2026-07-16T21:01:00Z: `git diff --check`, build with 0 warnings/errors, and the complete OM harness passed.

## P1 Coding Attractor Round 1

- Result: GAP. The catalog copied a persisted binding id but considered any callback at the same behavior key ready; registry entries did not carry identity and catalog reads did not capture an immutable snapshot.
- Result: GAP. Public positional records accepted `IReadOnlyList` values without defensive copying, so callers could retain and mutate the underlying collection.
- Controlled revision added T1.3 and clarified the design: readiness requires exact persisted/snapshot binding-id equality, and public catalog/result collections are immutable values. P2 still owns runtime execution gates, staged import and atomic publication.

## T1.3 Spot Check

- Registry state is now a copy-on-write immutable snapshot with serialized writes. Binding-aware overloads carry identity per callback slot; legacy APIs preserve executable behavior but carry no portable identity.
- Catalog captures one registry snapshot before async metadata reads and projects `Ready` only on exact Ordinal binding-id equality. Tests cover legacy no-id, mismatch, exact rebind, independent constraint slots and a registry update during catalog I/O.
- Public catalog, callback and diagnostic collections use defensively constructed `ImmutableArray<T>` values; retained source-list changes and interface downcast mutation attempts do not alter them.
- Parent verification at 2026-07-16T21:16:00Z: `git diff --check`, build with 0 warnings/errors, and the complete OM harness passed, including prior G3 overwrite/rollback behavior tests.

## P1 Coding Attractor Round 2

- Result: PASS. A fresh reviewer confirmed exact binding-id equality, legacy/mismatch unresolved behavior, single-snapshot catalog reads, and immutable public collections; no P1 scope or dependency drift remained.
- P1 gate passed with parent build/harness evidence and independent code/diff review. External `codument validate --strict` remains unavailable; `xmllint` and lifecycle checks are used as the local structural fallback.

## P2 Task Boundary Revision

- Moved concurrent pre/post import visibility from T2.0 to T2.2, where an import publication path exists and the test can be genuinely executable.
- T2.0 is now a green vertical slice for structured unresolved diagnostics and fail-closed behavior at every actual entry point; T2.1 adds the shared runtime gate and consistent snapshot resolution; T2.2 owns staged import, atomic publication and compensation.
- The desired semantics and D3b policy are unchanged; only implementation ordering changed so every leaf can retain a green main harness.

## T2.0 Spot Check

- Added public `BehaviorUnresolvedDiagnostic`/`BehaviorUnresolvedException` with deterministic code, kind, owner, stable key, slot, binding id and interceptor phase/seq fields.
- Exact persisted/snapshot readiness is enforced at constraint, computed direct/entity-view/as-of, action/parent, nearest mutation and exact inherited interceptor entry points. An unresolved child definition does not fall back; after interceptors are pre-resolved before before/action callbacks.
- Tests prove action/interceptor callbacks do not run, mutation/parent failures leave no committed writes, and legacy/native definitions without a binding row retain prior behavior.
- Parent spot-check found and repaired a T1.3 regression risk: G3 registration-failure compensation now restores complete registrations including nullable binding identities. New fault tests cover constraint when/then, action, mutation and exact interceptor readiness after rollback.
- Parent verification at 2026-07-16T21:40:00Z: `git diff --check`, build with 0 warnings/errors, and the complete OM harness passed.

## T2.1 Spot Check

- Added a shared cancellation-aware runtime gate, idempotent lease and immutable resolution scope. Runtime copies created with `with { Store = tx }` preserve the same gate.
- Catalog and every behavior entry point complete persistent metadata/binding plus registry-snapshot resolution under the gate, then release it before invoking user callbacks. Callback reentry is covered and does not deadlock.
- Action, parent action, inherited interceptors and returned mutations retain the same outer registry snapshot; cancellation while waiting is covered for catalog, constraints, computed reads/views/as-of, action and direct mutation.
- Internal publication is exposed only through a held lease for T2.2; no public mutable gate or registry snapshot API was added.
- Parent verification at 2026-07-16T22:00:00Z: `git diff --check`, build with 0 warnings/errors, and the complete OM harness passed.

## T2.2 Spot Check

- Added immutable typed callback sets, import options/results/diagnostics and a cancellation-aware JSON import facade. Manifest readiness is never trusted; exact typed binding identity alone stages executable callbacks.
- Pure decode, callback-index and owner/key preflight precede effects. Default import applies metadata/bindings with explicit unresolved diagnostics; `RequireReady` rejects missing, incompatible and unbound slots before DB or registry changes.
- All five metadata relations and binding identities apply in one write transaction under the runtime gate; one staged snapshot publication follows commit. Publish failure restores exact affected metadata/bindings before readers resume.
- Fault tests cover persistent failure, publish failure, failed compensation with preserved original plus compensation errors, cancellation, concurrent post-state visibility, sparse interceptor extension and SQLite restart projection.
- Parent verification at 2026-07-16T22:28:00Z: `git diff --check`, build with 0 warnings/errors, and the complete OM harness passed.

## T2.3 Spot Check

- Added `packages/Om.Portability.Yaml` with the only YamlDotNet reference, pinned to 18.1.0. The core assembly has no YAML dependency or reverse project reference.
- The adapter uses parser events and a bounded internal scalar/sequence/mapping AST, then delegates semantic decoding to `BehaviorManifestJsonCodec` and effects to the existing JSON import facade.
- UTF-8 bytes, depth and node limits apply before canonical catalog creation; multiple documents, explicit tags, anchors/aliases, merge keys, duplicate keys and non-string keys return stable structured diagnostics.
- Parent verification at 2026-07-16T22:43:00Z: restore, build with 0 warnings/errors, full OM harness and `git diff --check` passed; assembly-reference assertions prove dependency isolation.

## P2 Coding Attractor Round 1

- Result: GAP. Import validated the constraint type name and individual callback slot kinds but did not validate the complete type/slot shape.
- A `conditional` manifest containing only a typed validator could pass `RequireReady`, persist and publish without creating a conditional registration; runtime validation then silently skipped it.
- Controlled revision added T2.4: conditional/cross-entity/computed-dep must declare exactly `when` and `then`; custom must declare exactly `validator`. Invalid shapes fail preflight with zero effects in both default and strict modes.

## T2.4 Spot Check

- Import preflight now applies exact callback-slot coherence only after the constraint type is recognized: `conditional`, `cross-entity` and `computed-dep` require exactly `when` plus `then`; `custom` requires exactly `validator`.
- Stable `OMI1104` diagnostics expose the constraint identity and deterministic missing/extra slot lists. JSON permissive and `RequireReady` modes, plus the YAML adapter path, assert unchanged catalog bytes and the same registry snapshot on rejection.
- Legal conditional and custom manifests are imported together and `ValidateEntityAsync` proves `when`, `then` and `validator` callbacks execute instead of being silently skipped.
- Parent verification at 2026-07-16T22:51:00Z: `git diff --check`, build with 0 warnings/errors, and the complete OM harness passed.

## P2 Coding Attractor Round 2

- Result: GAP. A fresh reviewer found that direct/as-of property reads return an existing stored value before resolving the effective computed definition, while entity-view/current and as-of paths skip computed resolution when the property dictionary already contains that attribute.
- This lets an imported unresolved computed binding remain silent whenever the entity has a same-name stored value, violating the P2 fail-closed contract at all four computed read surfaces. Existing tests only covered entities without that stored value.
- Controlled revision added T2.5: resolve and validate computed readiness before accepting stored-value precedence; ready/unbound behavior should preserve the existing value precedence and invoke the callback only when no stored value exists.

## T2.5 Spot Check

- Current and as-of direct property reads now retain the stored result while resolving the effective computed owner and exact binding under one behavior-gate snapshot; unresolved bindings fail before that stored value can return.
- Current and as-of entity views resolve every effective computed definition even when its property already exists, but only queue callbacks for attributes missing from the stored property map. User callbacks run after the gate is released.
- Tests cover all four read surfaces with an inherited unresolved computed and same-name stored value, plus ready and legacy-unbound stored/missing value cases with exact callback counts.
- Parent verification at 2026-07-16T23:10:00Z: `git diff --check`, build with 0 warnings/errors, and the complete OM harness passed.

## P2 Coding Attractor Round 3

- Result: PASS. A fresh reviewer confirmed that all four computed read surfaces now validate inherited/effective binding readiness before stored-value precedence and invoke callbacks only after gate release when the stored value is absent.
- Import gating, transaction/publication compensation, constraint type-slot preflight, YAML canonical-pipeline delegation and dependency isolation remained intact.
- The reviewer classified complete cross-process and fault/interceptor matrices as P3 work rather than residual P2 gaps. P2 gate passed with the parent build/harness evidence from T2.5.

## T3.1 Spot Check

- The SQLite manifest-restart scenario now imports constraints with conditional `when`/`then` and custom `validator`, computed, action, mutation and exact `after/7` interceptor definitions owned by a base type and exercised through a child entity.
- Catalog bytes are compared at before-import, post-import, post-restart, partial-rebind and full-rebind stages, preserving exact metadata, callback slots, binding ids, phase/sequence and per-binding readiness.
- Restart with an empty registry fails closed at representative constraint, inherited computed/action/interceptor and mutation paths. Partial rebind reports exact remaining slots and executes no earlier callback; full exact-id rebind executes all five behavior kinds and all constraint callbacks.
- Parent verification at 2026-07-16T23:27:00Z: `git diff --check`, build with 0 warnings/errors, and the complete OM harness passed.

## T3.2 Spot Check

- Deterministically ordered state probes now separate canonical catalog, behavior metadata, persistent binding rows, registry binding identities, derived readiness and registry snapshot identity.
- Persistent failure and publish failure with successful compensation prove all state surfaces unchanged and preserve exact exception chains. Failed persistent compensation proves the expected split state explicitly and does not claim restoration.
- Concurrent readers remain blocked between persistent commit and publication/compensation, then observe exact complete post-state or compensated pre-state. Sparse interceptor `7/11/12` metadata, binding and registry identity are asserted; exact key conflicts fail facade preflight with zero effects.
- Unknown version/kind, invalid behavior/slot keys and duplicate/conflicting keys now pass through the import facade with exact diagnostics and zero effects. Equivalent JSON/YAML imports include one unresolved computed binding and prove identical diagnostics, unresolved identities and all state surfaces.
- Parent verification at 2026-07-16T23:48:00Z: `git diff --check`, build with 0 warnings/errors, and the complete OM harness passed.

## P3 Coding Attractor

- Result: PASS. A fresh reviewer independently confirmed the full SQLite restart/partial/full rebind matrix, separate four-surface state probes, exact fault chains and honest incomplete-compensation state.
- Concurrent success/failure visibility, sparse/conflicting interceptor keys, facade-level invalid input purity and unresolved JSON/YAML equivalence all matched the P3 acceptance boundary.
- P3 gate passed with parent build/harness evidence; final command, XML, scope and gap-loop checks remain P4 work.

## T4.1 Spot Check

- Fresh verifier and parent both passed `git diff --check`, XML well-formed checks, build with 0 warnings/errors and the full OM harness. The harness must run with `cozo-lib-dotnet` as cwd because its repository-file fixture discovery walks upward from the process directory; a parent run from the monorepo root failed only that fixture lookup, then passed from the package cwd.
- Om.Core and the root C# project contain no YamlDotNet or Jint dependency. YamlDotNet 18.1.0 is isolated to `packages/Om.Portability.Yaml`; no G9 validation facade was added.
- Existing `cozo-lib-dotnet-llm-wiki` changes remained untouched and semantically disconnected. Shared G3 runtime changes are owned by the completed sibling track.
- External `codument validate add-dotnet-om-behavior-portability --strict` is unavailable (`codument` command not found). `xmllint` plus lifecycle/status consistency checks are the recorded fallback, not a claimed strict-validator pass.

## P4 Coding Attractor Round 1

- Result: GAP. Import captures a registry snapshot under the behavior gate, but synchronous public registry writes publish under a separate registry lock. Unconditional staged publication can therefore overwrite a successful concurrent registration based on a newer snapshot.
- Result: GAP. The shared manifest field bag accepts metadata that is illegal for the selected behavior kind; persistence ignores those fields, so decode/import/re-export silently loses caller data instead of rejecting it.
- Controlled revision added T4.2 for expected-snapshot CAS publication with existing persistent compensation, and T4.3 for effect-free kind-specific metadata validation. These close correctness holes without changing user decisions or adding scope.

## T4.2 Spot Check

- Registry staged publication now compares the captured pre-import snapshot by reference and swaps under the same registry write lock used by synchronous registration APIs.
- A mismatch throws an internal deterministic publication-conflict exception and follows the existing persistent compensation path; no retry or unknown delegate merge is attempted.
- A deterministic post-commit barrier test proves synchronous registration does not wait on the async behavior gate, the concurrent callback/binding identity survives, metadata/bindings are compensated, derived readiness is unresolved against the preserved mismatched identity, and the gated reader resumes only after compensation.
- Parent verification at 2026-07-17T00:36:00Z: `git diff --check`, build with 0 warnings/errors, and the complete OM harness passed.

## T4.3 Spot Check

- Canonical decode now validates the exact metadata matrix once a behavior kind is known. Unknown kinds retain only their unknown-kind diagnostic; valid constraints require a string `constraintType`, while cross-kind fields produce field-path `OMM1203` diagnostics.
- Interceptor phase/sequence keeps the existing `OMM1201` key rule. Diagnostics retain deterministic code/path/message sorting, and legal five-kind canonical round-trip remains byte stable.
- Codec tests cover every illegal metadata field family. Facade tests prove four state surfaces unchanged, while equivalent YAML uses the same canonical JSON diagnostics and import rejection without adapter reinterpretation.
- Parent verification at 2026-07-17T00:49:00Z: `git diff --check`, build with 0 warnings/errors, and the complete OM harness passed.

## P4 Coding Attractor Round 2

- Result: PASS. A fresh reviewer confirmed expected-snapshot CAS publication closes the concurrent registry lost-update gap while preserving the existing gate and compensation semantics.
- The reviewer confirmed kind-specific metadata validation is effect-free, deterministic and shared by JSON/YAML, with legal canonical round-trip intact.
- Immutable public models, honest original/compensation errors and Jint/G9/YAML/llm-wiki scope boundaries remained valid. P4 now proceeds to its configured GapLoop.

## P4 GapLoop Round 1

- Result: FIX_APPLIED. A fresh skeptic found that native interceptor allocation only considered executable registry entries. A permissively imported unresolved sparse interceptor therefore preserved persistent metadata at `before/21` without a registry callback, but the next native registration could reuse a lower sequence.
- `NextInterceptorSeqAsync` now allocates from the maximum of persisted interceptor metadata and the current registry. The regression test proves unresolved `before/21` remains unresolved and callback-free while the native interceptor is placed at `before/22` with its own description.
- Parent spot-check confirmed exact owner/action/phase scoping and both metadata/registry maxima. Parent verification at 2026-07-17T01:05:00Z: `git diff --check`, build with 0 warnings/errors, and the complete OM harness passed.

## P4 GapLoop Round 2

- Result: NO_GAP. The required light recheck confirmed the Round 1 fix uses the persistent and registry maxima for the exact owner/action/phase and leaves the imported unresolved key intact.
- The fresh verifier ran `git diff --check` and the complete .NET OM harness; both passed. The configured two-round FIX_APPLIED recheck is closed.
