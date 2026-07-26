# P2 Attractor Check 001

## Verdict

`PASS`

No blocking or non-blocking P2 finding remains. The Bun and .NET runtime gates,
optional-package contracts, and Codument control-plane checks satisfy the
project/product attractors, the verification track contract, and the mission's
frozen D0-D5, D3a, and D3b decisions.

P3 may begin without reopening a decision.

## Issues First

No unresolved issue was found.

The first .NET full-harness attempt exposed a real shared-working-tree skew:
the CodeKnowledge implementation had advanced to schema v3 while its harness
asserted v2. That finding is resolved. The synchronized test now describes the
current v3 implementation, and the replacement verifier reached the full
harness success marker after executing YAML, CodeKnowledge, and all Jint
contract steps.

## Runtime Gates

### Bun

T2.1 records a fresh package-level run:

- `275 pass`, `0 fail`, `708 expect()` calls across 48 files;
- behavior focus: `23 pass`;
- permission focus: `13 pass`;
- schema focus: `12 pass`;
- existential focus: `26 pass`;
- public-surface focus: `29 pass`.

The package entry remains `"test": "bun test"`, so the full run discovers the
four T1.2 test repairs. The focused commands independently collected the G12
no-init list case, G13 fail-before-effect guardrails, and both G14 parent-clear
and direct value-inference cases.

### .NET

T2.2 records a fresh replacement run after the v3 synchronization:

- build succeeded with `0 warning` and `0 error`;
- the full diagnostics harness exited `0` and printed
  `Cozo.DotNet OM tests passed.`;
- all four focus values passed:
  `permission-governance`, `schema-evolution`,
  `existential-governance`, and `public-surface`;
- the YAML portability matrix completed at runtime;
- all 35 Jint contract steps completed before the harness success marker.

The test project references both optional packages directly:
`Om.Portability.Yaml` and `Om.Scripting.Jint`. The current full-harness entry
invokes `OmScriptingJintContractTests.RunAsync()`, while diagnostics bracket the
YAML portability matrix. These packages were executed, not merely compiled.

## CodeKnowledge V3 Boundary

The v2-to-v3 synchronization in `cozo-lib-dotnet/tests/Program.cs` is confined
to the CodeKnowledge schema lifecycle fixture:

- expected schema version changes from 2 to 3;
- expected relations add `ck_external_call` and `ck_semantic_claim`;
- repeated initialization is still asserted idempotent and data-preserving;
- legacy v1 detection remains fail-before-mutation;
- explicit reindex is expected to rebuild the current v3 schema.

This matches `CodeKnowledgeSchema.SchemaVersion = 3` and the current schema
definitions. The synchronization changes test expectations and names only; it
does not alter OM production code, parity APIs, authorization, migration
defaults, manifest serialization, scripting boundaries, or public
compatibility. The surrounding CodeKnowledge production evolution is
concurrent work outside this verification track and is not claimed as a
mission deliverable.

Scoped `git diff --check` passes for the reviewed .NET test,
CodeKnowledge, and Bun test paths.

## Control Plane

T2.3 resolves all eight bound mission links:

- the seven G3-G9 implementation tracks are `completed`;
- their task and task-group nodes are all `DONE`;
- all track and behavior-delta XML parses;
- every linked track passes Codument strict validation;
- no `DONE` mission task retains a candidate TrackLink;
- the verification track correctly remains `in_progress` while
  `VERIFY-T1` is `ACTIVE`.

The active verification track is therefore the only expected non-terminal
bound target. This is execution state, not a control defect.

## Freshness And Spot Checks

Evidence is fresh to the current shared working tree:

- T1.2 first repaired and focused-tested the four missing Bun cases.
- T2.1 then independently ran the Bun full suite and every requested focus
  group against those repairs.
- The first T2.2 verifier identified the CodeKnowledge skew rather than
  masking it.
- A replacement T2.2 verifier independently rebuilt and reran the full and
  focused .NET gates after the test synchronization.
- T2.3 independently re-resolved links and reran XML/strict controls.
- This attractor check re-read the current test entries, optional package
  references, v3 test diff, v3 production schema constants, decisions, and
  all P2 reports without rerunning the already duplicated full suites.

The parent-layer synchronization and scoped diff inspection are consistent
with the executors' reports. No report conclusion depends only on stale
linked-track prose.

## Attractor And Decision Alignment

The project attractor requires stable multi-language bindings, and the product
attractor treats build/test stability across primary bindings as a success
criterion. P2 supplies direct runtime evidence for both while preserving the
approved language-specific architecture.

No fresh evidence changes a frozen decision:

- D0/D4 observable parity and idiomatic C# APIs remain intact.
- D1 strict permission witness and fail-closed ABAC fixtures pass.
- D2 additive strict V2 schema-evolution contracts pass; the unrelated
  CodeKnowledge schema version is not the OM schema-evolution API.
- D3/D3a/D3b isolation, canonical JSON/YAML normalization, Jint execution, and
  unresolved-callback behavior remain exercised.
- D5 explicit `Keep | Set | Clear` behavior remains covered.

The verification stayed inside OM parity plus the minimum shared-harness test
synchronization needed to execute the gate. It did not expand into Wiki,
Java/Spring indexing, visualization, analytics, batch, or archival work.

## P3 Preconditions

P3 may start and should:

1. Publish one final issues-first G01-G14 matrix using the fresh P1 and P2
   evidence.
2. Keep all approved intentional differences tied to their frozen decision.
3. Verify report consistency, XML/strict state, scoped working-tree boundaries,
   and mission archive readiness.
4. Treat any new runtime contradiction as a gap or decision blocker rather
   than silently changing the contract.

## Residual Risk

The .NET project uses a large shared top-level diagnostics harness. Concurrent
CodeKnowledge evolution can invalidate unrelated expectations before later OM
steps run, as the initial v2/v3 skew demonstrated. Diagnostics now make the
failing stage visible, but the shared harness remains a repository
coordination risk.

P2 reports retain summarized command output rather than separate raw log
artifacts. The recorded commands, counts, completion markers, independent
replacement run, and current source wiring are sufficient for this gate, but a
future CI integration could preserve raw logs and split OM parity fixtures
from unrelated CodeKnowledge lifecycle tests.
