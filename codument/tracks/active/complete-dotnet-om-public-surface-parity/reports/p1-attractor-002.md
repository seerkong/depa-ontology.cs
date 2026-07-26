# P1 AttractorCheck 002

## Decision

`GAP`

This is a five-minute static direction review for G9/P1 `phase:after`. No full
suite was run and no source, track, or mission file was changed.

## Checks

- **Engineering boundary: GAP.** The coding profile resolves to the project
  attractor, whose stated .NET boundary is `cozo-lib-dotnet/`. The current
  diff in that boundary is not limited to the public-surface contract. For
  example, `CozoOm.cs:29-63` exposes behavior manifest import/export and
  schema-v2 APIs, while `OmModels.cs:163-260` adds schema diagnostics and
  migration models. These are outside the P1 public-surface delta and are
  unrelated capability additions in the same working-tree change.

- **Minimal public surface: GAP.** The intended P1 surface is the typed parent
  patch plus `ValidateConstraintsAsync`, `TraverseAsync`, and
  `InferValueType`, with compatibility wrappers. Those entries are present at
  `CozoOm.cs:65-69`, `CozoOm.cs:142-146`, and `CozoOm.cs:197-203`. However,
  the same diff also adds unrelated public behavior-catalog, schema-v2, and
  other runtime registration surfaces. The diff is therefore not a minimal
  public surface for this track and cannot be accepted as an isolated G9
  direction.

- **D0=A: PASS on direction/evidence.** `proposal.md`, `design.md`, and
  `delta.xml` consistently require observable parity with the Bun reference.
  The characterization and refresh reports explicitly cover Bun
  `defineType`, `validateConstraints`, `traverse`, and `inferValueType`, and
  record the null-inference correction to `Unknown`.

- **D5=C: PASS on direction/evidence.** The typed `Keep|Set|Clear` contract is
  defined at `OmModels.cs:37-78`; the legacy nullable overload remains at
  `CozoOm.cs:65-66` and is documented as null=Keep in the supplied reports.

- **P1 target/evidence: GAP.** `track.xml` defines P1 as characterization and
  contract, and the requested reports provide focused characterization,
  contract, and guardrail evidence. But `t1.2-refresh.md` and
  `t2.1-refresh.md` state that no unrelated source or fixture changes were
  made, while the current worktree has broad changes across Bun tests,
  dotnet-llm-wiki, and many additional .NET OM logic/model/runtime files. The
  evidence does not establish a clean, attributable G9/P1 diff.

## Recommendation

Bound the G9/P1 change set to the public-surface contract, its focused fixture,
and the registration guardrail paths. Separate the behavior-manifest,
schema-v2, permission/existential, and other capability changes into their own
track/worktree before re-running this AttractorCheck. Refresh the P1 reports
with the resulting attributable diff and evidence. Do not mark P1 PASS while
the current broad diff remains part of the reviewed change set.
