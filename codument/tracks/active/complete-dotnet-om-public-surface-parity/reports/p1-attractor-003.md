# P1 AttractorCheck 003

## Decision

`PASS`

Fresh `cdt:AttractorCheck` review for G9/P1 `phase:after`. The coding profile
resolves to the project attractor. This review applies the G9 attribution rule
in `analysis/scope-boundary.md`: unrelated concurrent changes in the shared
working tree, including unrelated hunks in otherwise attributable files, are
environmental context and are not classified as G9 gaps.

## Checks

- **Engineering boundary: PASS.** The attributable G9 public-surface symbols
  are present in the listed OM Core files, with the focused fixture under the
  track. The relevant changes are limited to typed parent patches, public
  validation/traversal/inference facades, their logic delegation, and
  fail-before-effect registration paths. Unrelated Bun, wiki, schema,
  permission, existential, action, portability, and other mission changes were
  excluded as required by the scope boundary.

- **Minimal public surface: PASS.** `CozoOm` exposes the typed
  `DefineTypeAsync(DefineTypePatchInput)` facade,
  `ValidateConstraintsAsync`, `TraverseAsync`, and `InferValueType`. The
  compatibility nullable `DefineTypeAsync` overload remains available. The
  reviewed fixture checks public discoverability and the observable
  Keep/Set/Clear, validation, traversal, and inference behavior without
  requiring unrelated public APIs for the G9 claim.

- **D0=A: PASS.** `proposal.md`, `design.md`, and
  `behavior_deltas/public-surface/delta.xml` consistently require observable
  parity with the Bun capability. `t1.2-refresh.md` records the Bun-aligned
  null/JSON-null mapping to `Unknown`; the focused fixture covers string,
  number, bool, and null inference, parent updates, constraint validation, and
  outgoing traversal. The supplied Bun characterization evidence reports 34
  passing expectations.

- **D5=C: PASS.** `TypeParentPatch` gives distinct `Keep`, `Set(parent)`, and
  `Clear` intents, while the legacy nullable overload preserves `null=Keep`.
  Owner, constraint-scope, and interceptor-phase validation occurs before
  persistent metadata or callback registry effects. The relevant callback
  registration paths compensate metadata and registry state on a later
  registration failure. The focused fixture asserts no persistent or callback
  ghosts for invalid owner, scope, and phase inputs.

- **P1 target/evidence: PASS.** P1 is the characterization and contract phase;
  `track.xml` marks T1.1 and T1.2 complete, and the supplied refresh reports
  document the contract, implementation audit, and focused evidence. A fresh
  local run of the declared .NET commands succeeded:

  ```text
  MSBUILDTERMINALLOGGER=off /usr/local/share/dotnet/dotnet build tests/Cozo.DotNet.Om.Tests.csproj -v:q
  Build succeeded. 0 Warning(s), 0 Error(s).

  COZO_OM_TEST_FOCUS=public-surface MSBUILDTERMINALLOGGER=off /usr/local/share/dotnet/dotnet run --project tests/Cozo.DotNet.Om.Tests.csproj --no-build
  Focused public surface parity fixture passed.
  ```

## Environment Background

The shared worktree contains concurrent changes outside G9 and additional
unrelated hunks in some G9-attributable files. Per `scope-boundary.md`, these
were not used as G9 evidence and do not affect this decision.

## Recommendation

No G9/P1 direction gap remains within the attributable public-surface scope.
Proceed with the track's P2 implementation/verification workflow; do not infer
that this P1 check completes T2.1 or T2.2.
