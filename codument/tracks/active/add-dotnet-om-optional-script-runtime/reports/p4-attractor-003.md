# P4 AttractorCheck 003

Date: 2026-07-17

Profile: `coding` from `codument/config/attractor-profiles.xml`

Attractor: `codument/attractors/project.md`

Reviewer role: fresh read-only AttractorCheck reviewer

## Conclusion

**PASS**

T4.3's harness observability change remains in the test harness, is opt-in,
and does not change the OM runtime or script adapter behavior. It preserves
the project boundary: Jint remains an optional adapter dependency rather than
an `Om.Core` concern. No coding-attractor gap was found in the reviewed scope.

## Scope And Inputs Reviewed

- `track.xml`, `proposal.md`, `design.md`, and
  `behavior_deltas/cozo-dotnet-om/delta.xml`.
- `analysis/findings.md`, `reports/verify-report.md`, `reports/reopen-001.md`,
  `reports/harness-diagnosis-001.md`, and `reports/t4.3-parent-spot-check.md`.
- The uncommitted harness changes in
  `cozo-lib-dotnet/tests/Program.cs` and
  `cozo-lib-dotnet/tests/OmScriptingJintContractTests.cs`, plus the related
  test project and adapter project references.

## Evidence

1. **Opt-in diagnostics and default behavior**

   `HarnessDiagnostics` is test-only at
   `cozo-lib-dotnet/tests/Program.cs:7276`. It is enabled only for an exact
   `COZO_OM_TEST_DIAGNOSTICS=1` value (`Program.cs:7279-7286`). With the switch
   absent, the static timer field is `null`, and `Start` and `Complete` return
   before writing output (`Program.cs:7283-7286`, `Program.cs:7290-7313`). With
   the switch present, it writes only
   `START`, five-second `WAIT`, and `COMPLETE` records to stderr
   (`Program.cs:7302`, `Program.cs:7321-7338`), so normal harness stdout and
   completion behavior remain unchanged.

2. **No test weakening or control-flow substitution**

   The complete harness still calls the Jint matrix exactly once at
   `cozo-lib-dotnet/tests/Program.cs:7172-7175`. Its wrappers in
   `cozo-lib-dotnet/tests/OmScriptingJintContractTests.cs:55-65` only announce
   the existing assertion-group name and immediately invoke/await the original
   delegate. They introduce no catch-and-continue path, filter, timeout,
   cancellation token, skip, or changed assertion. The full pre-existing Jint
   matrix remains enumerated by direct calls at lines `19-52`.

3. **Timeout diagnosis and threshold preservation**

   T4.3 explicitly requires opt-in last-step timing while retaining the
   external 120-second threshold and existing coverage
   (`track.xml:169-174`). The reopen report forbids threshold relaxation,
   skipped tests, and weakened coverage
   (`reports/reopen-001.md:16-18`). The new heartbeat identifies the active
   top-level matrix or individual Jint assertion group; this meets the missing
   timeout-observability condition recorded in `reports/verify-report.md`.
   `reports/harness-diagnosis-001.md:69-86` records five independent external
   watchdog exits of `0`, all below 120 seconds, with no matching residual
   process. Its lines `42-44` also state that no test/callback/resource probe
   was skipped or weakened and no Jint or OM runtime behavior was changed to
   improve timing. The independently authored parent spot check reaches the
   same code-review result at `reports/t4.3-parent-spot-check.md:9-11`.

4. **Jint and script dependency boundary**

   The test project references the optional adapter only
   (`cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj:13`). The adapter points
   back to the root .NET project and is the sole reviewed project with a pinned
   `Jint` package reference (`cozo-lib-dotnet/packages/Om.Scripting.Jint/Cozo.DotNet.Om.Scripting.Jint.csproj:16-17`). A fresh scoped scan found no
   `Jint`, `Om.Scripting.Jint`, `JintBehaviorScript`, script-source, or
   script-definition reference in `cozo-lib-dotnet/src/Om.Core`. Thus the
   diagnostics do not leak the optional engine or a script schema into
   `Om.Core`.

## Fresh Checks Run

From `cozo-lib-dotnet`:

```sh
/usr/local/share/dotnet/dotnet build tests/Cozo.DotNet.Om.Tests.csproj --no-restore
```

Result: exit `0`, 0 warnings, 0 errors.

Also passed:

```sh
git diff --check -- cozo-lib-dotnet/tests/Program.cs \
  cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj
xmllint --noout codument/tracks/add-dotnet-om-optional-script-runtime/track.xml \
  codument/tracks/add-dotnet-om-optional-script-runtime/behavior_deltas/cozo-dotnet-om/delta.xml \
  codument/config/attractor-profiles.xml
```

No source, track, mission, or status file was modified by this review.
