# P2 AttractorCheck 001

## Decision

`PASS`

Fresh `cdt:AttractorCheck` review for G9/P2 `phase:after`. The `coding`
profile resolves to `codument/attractors/project.md`. Attribution follows
`analysis/scope-boundary.md`; concurrent Bun, ontology/wiki, schema,
permission, existential, action, portability, scripting, and other mission
changes are environmental context and are not classified as G9 gaps.

## Checks

- **G9 attributable boundary: PASS.** The reviewed public-surface work is
  represented by the declared typed parent contracts, `CozoOm` facades,
  corresponding core logic, conversion inference, and
  `PublicSurfaceParityFixtures.cs`. Unrelated files and unrelated hunks in
  shared attributable files were excluded per the explicit scope boundary.
  No G9 scope violation is charged.

- **Typed parent updates: PASS.** `TypeParentPatch` exposes distinct
  `Keep`, `Set(parentType)`, and `Clear` intents, rejects an empty `Set`, and
  `DefineTypeAsync(DefineTypePatchInput)` dispatches those intents without
  overloading null. The legacy nullable overload remains available with
  null-as-Keep compatibility. Evidence: `OmModels.cs:37-78`,
  `CozoOm.cs:65-69`, `TypeLogic.cs:23-37`, and fixture assertions at
  `PublicSurfaceParityFixtures.cs:17-65`.

- **D0=A and public shape: PASS.** `CozoOm` exposes idiomatic public
  `ValidateConstraintsAsync`, `TraverseAsync`, and `InferValueType` methods
  with cancellation on effectful operations, while conversion and traversal
  remain delegated to existing logic. The public surface is limited to the
  requested capability and compatibility wrapper. Evidence:
  `CozoOm.cs:142-146,197-203`, `RelationLogic.cs:136-175`, and
  `OmConvert.cs:44-60`.

- **D5=C and fail-before-effect guardrails: PASS.** The focused fixture
  rejects missing owners, invalid constraint scope, and invalid interceptor
  phase, then asserts both persistent metadata and callback registry entries
  are absent. The supplied T2.1/T2.2 reports additionally document
  pre-write validation and rollback compensation for later registration
  failures. No ghost-registration behavior was observed.

- **Acceptance and evidence: PASS.** `track.xml` marks T2.1 and T2.2 done
  with their acceptance criteria checked. Fresh verification in this checkout
  produced:

  ```text
  dotnet build tests/Cozo.DotNet.Om.Tests.csproj -v:q
  Build succeeded. 0 Warning(s). 0 Error(s).

  COZO_OM_TEST_FOCUS=public-surface dotnet run --project tests/Cozo.DotNet.Om.Tests.csproj --no-build
  Focused public surface parity fixture passed.

  git diff --check
  no output

  codument validate complete-dotnet-om-public-surface-parity --strict
  track.xml OK + 1 behavior delta(s)

  xmllint --noout track.xml behavior_deltas/public-surface/delta.xml
  passed
  ```

- **Unrelated refactoring or breaking behavior: PASS within scope.** The
  reviewed G9 symbols do not introduce an unrelated public API or a breaking
  removal; the explicitly retained legacy overload covers migration
  compatibility. Large concurrent changes visible elsewhere in the shared
  worktree, including unrelated hunks in otherwise attributable files, are
  excluded by the declared attribution rule and therefore do not change the
  P2 decision.

## Conclusion

No attributable G9/P2 direction gap remains. T2.1 and T2.2 acceptance is
supported by the implementation, focused fixture, supplied refresh reports,
and fresh local validation.
