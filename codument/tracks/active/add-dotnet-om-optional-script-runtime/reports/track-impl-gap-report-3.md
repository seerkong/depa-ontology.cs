# Gap Loop Round 3 Report: add-dotnet-om-optional-script-runtime

Scope: phase `P4` (full target-backward review)

Round metadata: `track.xml` records `<gap-round>3</gap-round>` and P4 retains
`cdt:GapLoop max-rounds="3" on-exhausted="block" verify-round="true"`.

Verdict: NO_GAP

## Inputs And Method

This fresh round reread the required target surface rather than treating prior
reports as truth:

- `proposal.md`, `design.md`, `track.xml`, `decisions.md`, and
  `analysis/findings.md`;
- `behavior_deltas/cozo-dotnet-om/delta.xml` (one requirement, three suites,
  fourteen unique cases);
- every extant report, with particular attention to `verify-report.md`,
  `reopen-001.md`, `harness-diagnosis-001.md`,
  `t4.3-parent-spot-check.md`, and `p4-attractor-003.md`;
- the current implementation, test harness, adapter package, and uncommitted
  diff.

The review was target-backward: P4/T4.3's diagnostic and external-watchdog
requirements were checked first, then the behavior matrix, script-safety
boundary, package isolation, and final-validation claims were traced to the
current executable harness and source.

## Fresh T4.3 Evidence

From `cozo-lib-dotnet`:

```sh
MSBUILDTERMINALLOGGER=off /usr/local/share/dotnet/dotnet build \
  tests/Cozo.DotNet.Om.Tests.csproj --no-restore
```

Result: exit 0, 0 warnings, 0 errors.

A fresh diagnostic run used the required external 120-second alarm:

```sh
/usr/bin/time -lp perl -e 'alarm shift; exec @ARGV' 120 \
  env COZO_OM_TEST_DIAGNOSTICS=1 \
  /usr/local/share/dotnet/dotnet run \
  --project tests/Cozo.DotNet.Om.Tests.csproj --no-build
```

The child harness exited successfully in `47.93s`. Its opt-in stderr records
identified every active top-level matrix and every Jint assertion group. The
slowest observed top-level step was `DEPA red-light detection` at `12.788s`;
the slowest Jint group was
`Jint.AssertP2FiniteDefaultsBoundPendingPromisesAsync` at `2.003s`. A
post-child shell bookkeeping line attempted to assign zsh's reserved `status`
variable and therefore made that wrapper return 1 after the successful child
completed; it did not affect the supervised harness result and was not used as
the acceptance verdict.

Five further independent default-mode runs used the same 120-second external
alarm. All printed `Cozo.DotNet OM tests passed.` and exited 0:

| Run | Wall time | Exit |
| --- | ---: | ---: |
| 1 | 44s | 0 |
| 2 | 47s | 0 |
| 3 | 49s | 0 |
| 4 | 45s | 0 |
| 5 | 60s | 0 |

All are below the 120-second bound. A separate post-run exact process scan for
`Cozo.DotNet.Om.Tests.dll` and its test-bin invocation returned no process.
The loop-local broad scan was deliberately not used as residual evidence
because it also matched its own supervising shell command line.

`HarnessDiagnostics` is enabled only by the exact
`COZO_OM_TEST_DIAGNOSTICS=1` value, emits `START`/five-second `WAIT`/
`COMPLETE` records only to stderr, and is default-silent. `Program.cs` marks
all top-level matrices; `OmScriptingJintContractTests.RunAsync()` calls all 34
existing assertion groups through wrappers that immediately execute the
original delegates. The wrappers contain no catch-and-continue path, filter,
timeout, cancellation substitution, or skipped assertion.

## Goal Coverage And Boundaries

- The behavior delta still contains exactly 14 unique cases. The complete OM
  harness runs the Jint contract matrix, including all callback shapes,
  readiness/import failures, host capabilities, JSON conversion, parent action,
  rollback, fresh engines, ambient denial, resource limits, cancellation,
  error redaction, and package isolation.
- Focused source scans found no `AllowClr`, synchronous `.Result`/`.Wait()` or
  `GetAwaiter().GetResult()` bridge, filesystem/network API, module-loader
  surface, or script/Jint schema in `src/Om.Core`.
- The optional adapter remains the only package that pins Jint; the test project
  references that adapter, and neither the root project nor `Om.Core` carries a
  Jint dependency.
- `xmllint --noout` passed for the track, behavior delta, adapter project, and
  test project. `codument validate add-dotnet-om-optional-script-runtime
  --strict` passed. Scoped tracked and untracked whitespace checks passed.

## Conclusion

The prior verifier's 120-second failure and missing location evidence are
closed by the current test-only opt-in observability and fresh five-run
external-watchdog evidence. No test was weakened, no behavior case was
removed or bypassed, and no script-safety, package-isolation, or final
validation regression was found.

No implementation, `track.xml`, design, or behavior delta change was needed
in this round. This report is the only file added by the round.
