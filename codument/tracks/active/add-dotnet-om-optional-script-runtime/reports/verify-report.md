# Verification Report: add-dotnet-om-optional-script-runtime

Verified at: `2026-07-17T05:48:34Z`

Verifier role: fresh independent `codument-verify` executor for mission node `G5-T2`.

Scope: whole completed track. Existing reports were treated only as leads. This pass was read-only except for this report.

## Blocking Issues

- `T3.4-AC1` / `T4.1-AC1`: the complete OM harness did not satisfy the required external 120-second watchdog. A fresh 120-second supervised run of `/usr/local/share/dotnet/dotnet run --project tests/Cozo.DotNet.Om.Tests.csproj --no-build` timed out, was killed as process group `EXIT_CODE=143`, and produced no test output before termination. A diagnostic 180-second supervised run completed successfully only after `122.866s`, proving the harness can pass but currently exceeds the explicit 120-second bound in this environment. Impact: the track cannot be accepted as completed for G5-T2. Suggested next step: return to implementation/gap-loop to reduce or isolate the slow path and re-run five consecutive 120-second watchdog passes.
- `T3.4-AC2`: the timeout path did not identify the concrete test or wait point. The executable harness only prints `Cozo.DotNet OM tests passed.` at process end, so the 120-second timeout run had an empty output tail and no focused location evidence. Impact: the "if hang/timeout reproduces, locate the specific test/waiting point" acceptance is not met. Suggested next step: add bounded per-probe progress/diagnostic output or otherwise isolate the slow test in an implementation pass.

## Non-Blocking Issues

- None found beyond the blocking watchdog failure.

## Command Evidence

### Toolchain

Command:

```sh
/usr/local/share/dotnet/dotnet --info
```

Evidence: .NET SDK `10.0.103`, runtime `10.0.3`, `osx-arm64`.

### Restore And Build

Working directory: `cozo-lib-dotnet`

Commands:

```sh
/usr/local/share/dotnet/dotnet restore tests/Cozo.DotNet.Om.Tests.csproj
MSBUILDTERMINALLOGGER=off /usr/local/share/dotnet/dotnet build tests/Cozo.DotNet.Om.Tests.csproj --no-restore
```

Outcome:

```text
All projects are up-to-date for restore.
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:03.21
```

### Required 120-Second Watchdog Harness

The harness was launched by a Python supervisor with `start_new_session=True`, `communicate(timeout=120)`, process-group termination on timeout, and residual-process checks.

Command under supervisor:

```sh
/usr/local/share/dotnet/dotnet run --project tests/Cozo.DotNet.Om.Tests.csproj --no-build
```

Outcome:

```text
RUN=1
WATCHDOG_TIMEOUT=1
EXIT_CODE=143
DURATION_SECONDS=120.025
RESIDUAL_DOTNET_TEST_PROCESS_COUNT=0
OUTPUT_TAIL_START

OUTPUT_TAIL_END
```

Final follow-up residual check:

```text
pgrep -af "Cozo.DotNet.Om.Tests|tests/Cozo.DotNet.Om.Tests.csproj"
```

Outcome: exit `1`, no matching residual process.

### Diagnostic 180-Second Harness

This run does not satisfy the 120-second acceptance; it was used only to distinguish permanent hang from boundary overrun.

Outcome:

```text
WATCHDOG_LIMIT_SECONDS=180
WATCHDOG_TIMEOUT=0
EXIT_CODE=0
DURATION_SECONDS=122.866
RESIDUAL_DOTNET_TEST_PROCESS_COUNT=0
OUTPUT_TAIL_START
Cozo.DotNet OM tests passed.
OUTPUT_TAIL_END
```

The Jint contract suite is wired into the complete OM harness at `cozo-lib-dotnet/tests/Program.cs:7140`, and `OmScriptingJintContractTests.RunAsync()` invokes the focused script-runtime checks at `cozo-lib-dotnet/tests/OmScriptingJintContractTests.cs:17-52`.

### Dependency, Schema, XML, Codument, And Diff Checks

Commands and outcomes:

```sh
/usr/local/share/dotnet/dotnet list cozo-lib-dotnet/packages/Om.Scripting.Jint/Cozo.DotNet.Om.Scripting.Jint.csproj package --include-transitive
```

```text
Top-level Package      Requested   Resolved
> Jint                 4.13.0      4.13.0

Transitive Package      Resolved
> Acornima              1.6.2
```

```sh
xmllint --noout \
  codument/tracks/add-dotnet-om-optional-script-runtime/track.xml \
  codument/tracks/add-dotnet-om-optional-script-runtime/behavior_deltas/cozo-dotnet-om/delta.xml \
  cozo-lib-dotnet/packages/Om.Scripting.Jint/Cozo.DotNet.Om.Scripting.Jint.csproj \
  cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj
```

Outcome: exit `0`, no XML errors.

```sh
codument validate add-dotnet-om-optional-script-runtime --strict
```

```text
✓ add-dotnet-om-optional-script-runtime: track.xml OK + 1 behavior delta(s)
```

```sh
git diff --check -- cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj cozo-lib-dotnet/tests/Program.cs cozo-lib-dotnet/src/Om.Core cozo-lib-dotnet-llm-wiki codument/tracks/add-dotnet-om-optional-script-runtime
```

Outcome: exit `0`, no whitespace errors.

Relevant untracked adapter/test/track whitespace scan outcome:

```text
UNTRACKED_RELEVANT_WHITESPACE_FAILURES=0
```

Dependency/schema/security scans:

```text
rg -n -i "Jint|Acornima|Om\.Scripting\.Jint" cozo-lib-dotnet --glob '*.csproj' ...
```

Hits were limited to:

- `cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj:13` project reference to the adapter.
- `cozo-lib-dotnet/packages/Om.Scripting.Jint/Cozo.DotNet.Om.Scripting.Jint.csproj:17` pinned `Jint` package reference.

Scans for `AllowClr`, `.Result`, `.Wait(`, `GetAwaiter().GetResult(`, module loader, filesystem/network APIs in `cozo-lib-dotnet/packages/Om.Scripting.Jint` and `cozo-lib-dotnet/src/Om.Core` returned no hits. Scans for script schema / Jint terms in `cozo-lib-dotnet/src/Om.Core` returned no hits.

## Acceptance Matrix

| Criterion | Exists | Substantive | Wired | Verdict | Evidence |
| --- | --- | --- | --- | --- | --- |
| `T1.1-AC1` Jint isolated to adapter | PASS | PASS | PASS | PASS | Adapter package pins Jint `4.13.0`; root/Om.Core scans have no Jint/script schema; executable isolation assertions exist at `OmScriptingJintContractTests.cs:55-96`; 180s harness passed. |
| `T1.1-AC2` all slots map to typed collections | PASS | PASS | PASS | PASS | Slot mapping code at `JintBehaviorScriptProvider.cs:820-840`; tests at `OmScriptingJintContractTests.cs:144-164`; 180s harness passed. |
| `T1.1-AC3` missing/duplicate/incompatible/native conflicts diagnose before effects | PASS | PASS | PASS | PASS | Tests at `OmScriptingJintContractTests.cs:167-240` and native conflict coverage later in the same suite; 180s harness passed. |
| `T1.2-AC1` legal catalog/scripts generate exact typed callbacks for import | PASS | PASS | PASS | PASS | Provider `BuildBindings` exists at `JintBehaviorScriptProvider.cs:175-220`; canonical import execution tests at `OmScriptingJintContractTests.cs:1490-1555`; 180s harness passed. |
| `T1.2-AC2` provider failure does not mutate OM state | PASS | PASS | PASS | PASS | Effect-free provider validation tests exist at `OmScriptingJintContractTests.cs:341-428` and `592-696`; 180s harness passed. |
| `T1.2-AC3` immutable contracts and no Jint boundary leak | PASS | PASS | PASS | PASS | Immutable records at `JintBehaviorScriptProvider.cs:19-158`; package-only engine references; 180s harness passed. |
| `T1.3-AC1` RequireReady callbacks execute, no placeholder readiness | PASS | PASS | PASS | PASS | Full suite entry includes `AssertRequireReadyBindingsExecuteScriptsAsync`; 180s harness passed. |
| `T1.3-AC2` blank/null/invalid inputs produce stable diagnostics before effects | PASS | PASS | PASS | PASS | Tests at `OmScriptingJintContractTests.cs:429-556` and `698-748`; 180s harness passed. |
| `T1.3-AC3` reversed inputs produce stable diagnostic/binding order | PASS | PASS | PASS | PASS | Test entry includes `AssertStableOrderingAcrossReversedInputs`; 180s harness passed. |
| `T1.4-AC1` invalid/non-callable source fails preflight before ready | PASS | PASS | PASS | PASS | Test entry includes invalid-source preflight; code uses bounded preflight path; 180s harness passed. |
| `T1.4-AC2` null/default graph returns diagnostics not raw exceptions | PASS | PASS | PASS | PASS | Test entry includes null-element diagnostics; 180s harness passed. |
| `T1.4-AC3` callable preflight does not execute function body | PASS | PASS | PASS | PASS | Test entry includes `AssertCallablePreflightDoesNotExecuteFunctionBodyAsync`; 180s harness passed. |
| `T2.1-AC1` all callback shapes convert typed results | PASS | PASS | PASS | PASS | Result-shape test entry at `OmScriptingJintContractTests.cs:33`; conversion code at `JintBehaviorScriptProvider.cs:1179-1339`; 180s harness passed. |
| `T2.1-AC2` invalid CLR/JSON and compile/runtime errors are stable and redacted | PASS | PASS | PASS | PASS | Error mapping code at `JintBehaviorScriptProvider.cs:1346-1462`; tests invoked by full suite; README documents codes; 180s harness passed. |
| `T2.1-AC3` fresh engine / no global leak | PASS | PASS | PASS | PASS | Invocation creates a new engine at `JintBehaviorScriptProvider.cs:847-873`; concurrency test invoked by full suite; 180s harness passed. |
| `T2.2-AC1` loop/recursion/statement/memory limits stop or reject | PASS | PASS | PASS | PASS | Bounded preflight tests at `OmScriptingJintContractTests.cs:592-696`; limit mapping at `JintBehaviorScriptProvider.cs:1376-1421`; 180s harness passed. |
| `T2.2-AC2` cancellation maps as cancellation | PASS | PASS | PASS | PASS | Cancellation mapping at `JintBehaviorScriptProvider.cs:1353-1359`; cancellation tests invoked by full suite; 180s harness passed. |
| `T2.2-AC3` no cross-invocation state, shared objects immutable | PASS | PASS | PASS | PASS | Fresh engine and frozen JSON/host code at `JintBehaviorScriptProvider.cs:847-873` and `1553-1589`; 180s harness passed. |
| `T2.3-AC1` host allowlist denies CLR/filesystem/network/module/unknown | PASS | PASS | PASS | PASS | Host factory exposes only fixed methods at `JintBehaviorScriptProvider.cs:931-1055` and `1566-1589`; ambient scans clean; 180s harness passed. |
| `T2.3-AC2` async host/Promise awaited without sync bridge | PASS | PASS | PASS | PASS | `UnwrapIfPromiseAsync` awaited at `JintBehaviorScriptProvider.cs:873`; sync-bridge scan returned no hits; 180s harness passed. |
| `T2.3-AC3` parent action composition and transaction host writes | PASS | PASS | PASS | PASS | Parent/transaction tests at `OmScriptingJintContractTests.cs:1820-1946`; 180s harness passed. |
| `T2.4-AC1` finite defaults and pending Promise timeout | PASS | PASS | PASS | PASS | Defaults at `JintBehaviorScriptProvider.cs:59-69`; Promise timeout test invoked by full suite; 180s harness passed. |
| `T2.4-AC2` no skipConstraints and public error redaction | PASS | PASS | PASS | PASS | README states only `validTime` option at lines `128-135`; host/error tests invoked by full suite; 180s harness passed. |
| `T2.4-AC3` stable resource classification and source location | PASS | PASS | PASS | PASS | Location extraction at `JintBehaviorScriptProvider.cs:1442-1462`; 180s harness passed. |
| `T2.5-AC1` bounded callable preflight | PASS | PASS | PASS | PASS | Preflight limit tests at `OmScriptingJintContractTests.cs:592-696`; 180s harness passed. |
| `T2.5-AC2` hostile factory returns bounded diagnostic and no effects | PASS | PASS | PASS | PASS | Hostile factory cases at `OmScriptingJintContractTests.cs:601-671`; 180s harness passed. |
| `T2.5-AC3` legal function/arrow compatibility | PASS | PASS | PASS | PASS | Valid callback/import tests invoked by full suite; 180s harness passed. |
| `T3.1-AC1` six callbacks ready, missing scripts unresolved/RequireReady zero effect | PASS | PASS | PASS | PASS | Canonical manifest tests invoked by full suite; 180s harness passed. |
| `T3.1-AC2` native/script coexistence and same-id conflict | PASS | PASS | PASS | PASS | Native/script docs at README lines `204-215`; tests invoked by full suite; 180s harness passed. |
| `T3.1-AC3` ambient denial/resource/cancellation executable assertions | PASS | PASS | PASS | PASS | Sandbox/resource tests invoked by full suite; 180s harness passed. |
| `T3.2-AC1` parent/inheritance order matches C# runtime | PASS | PASS | PASS | PASS | Test code at `OmScriptingJintContractTests.cs:1820-1946`; 180s harness passed. |
| `T3.2-AC2` failure rollback covers effects | PASS | PASS | PASS | PASS | Failure matrix at `OmScriptingJintContractTests.cs:1948-2061`; 180s harness passed. |
| `T3.2-AC3` cancellation prevents late effects | PASS | PASS | PASS | PASS | Cancellation rollback/no-late-effect test at `OmScriptingJintContractTests.cs:2063-2125`; 180s harness passed. |
| `T3.3-AC1` docs state in-process defense-in-depth, not OS sandbox | PASS | PASS | PASS | PASS | README lines `231-241`. |
| `T3.3-AC2` source is programmatic; Om.Core/canonical manifest has no script schema/Jint | PASS | PASS | PASS | PASS | README lines `8-19`; Om.Core scans clean; canonical schema test at `OmScriptingJintContractTests.cs:2342-2362`; 180s harness passed. |
| `T3.4-AC1` five consecutive full harness runs under 120s and no residual process | PASS | FAIL | FAIL | FAIL | First fresh 120s run timed out at `120.025s`; no residual process. Requirement for five consecutive exits `0` is not met. |
| `T3.4-AC2` timeout/hang isolated to concrete test/wait point | PASS | FAIL | FAIL | FAIL | 120s timeout produced empty output tail; no test/wait point was identified. |
| `T3.4-AC3` duration/status/cleanup recorded and build/diff/XML pass | PASS | PASS | PASS | PASS | This report records 120s and 180s durations/status/cleanup; build, XML, Codument strict, and diff checks passed. |
| `T4.1-AC1` build and complete OM harness exit 0, XML/diff pass | PASS | FAIL | FAIL | FAIL | Build/XML/diff passed, but the required 120s complete harness timed out. |
| `T4.1-AC2` Jint only in adapter; no sync host bridge | PASS | PASS | PASS | PASS | Package scan and sync-bridge scan clean; Jint direct reference only in adapter project. |
| `T4.1-AC3` all behavior cases have executable or structural evidence | PASS | PASS | PASS | PASS | All 14 cases map to `OmScriptingJintContractTests.RunAsync()` and the diagnostic full harness passed at `122.866s`; the 120s harness failure remains separately blocking. |
| `T4.2-AC1` cross-slot reuse returns `OMS1002`, zero bindings | PASS | PASS | PASS | PASS | Cross-slot test starts at `OmScriptingJintContractTests.cs:211`; README lines `210-215`; 180s harness passed. |
| `T4.2-AC2` same-slot reuse remains compatible | PASS | PASS | PASS | PASS | Same-slot reuse covered in `AssertCrossSlotReuseRejectsAndSameSlotReuseSharesAsync`; 180s harness passed. |
| `T4.2-AC3` fail closed before import/effects and README documents restriction | PASS | PASS | PASS | PASS | README lines `210-215`; provider/effect-free tests invoked by full suite; 180s harness passed. |

## Behavior Case Matrix

| Behavior case | Exists | Substantive | Wired | Verdict | Evidence |
| --- | --- | --- | --- | --- | --- |
| `binding/all-callback-shapes` | PASS | PASS | PASS | PASS | Slot mapping tests at `OmScriptingJintContractTests.cs:144-164`; 180s harness passed. |
| `binding/missing-script` | PASS | PASS | PASS | PASS | Missing-script diagnostics at `OmScriptingJintContractTests.cs:167-184`; 180s harness passed. |
| `binding/incompatible-reuse` | PASS | PASS | PASS | PASS | Incompatible reuse test at `OmScriptingJintContractTests.cs:186-196`; 180s harness passed. |
| `binding/native-coexistence` | PASS | PASS | PASS | PASS | Native/script coexistence is in `AssertNativeAndScriptBindingsMergeDeterministically`; 180s harness passed. |
| `execution/context-capabilities` | PASS | PASS | PASS | PASS | Host read/write capability tests at `OmScriptingJintContractTests.cs:817-1128`; 180s harness passed. |
| `execution/result-conversion` | PASS | PASS | PASS | PASS | Result shape/conversion tests invoked by full suite; conversion code at `JintBehaviorScriptProvider.cs:1179-1339`; 180s harness passed. |
| `execution/parent-action-composition` | PASS | PASS | PASS | PASS | Parent composition test at `OmScriptingJintContractTests.cs:1820-1946`; 180s harness passed. |
| `execution/transaction-rollback` | PASS | PASS | PASS | PASS | Rollback matrix at `OmScriptingJintContractTests.cs:1948-2125`; 180s harness passed. |
| `execution/fresh-engine` | PASS | PASS | PASS | PASS | New engine per invocation at `JintBehaviorScriptProvider.cs:847-873`; concurrency test invoked by full suite; 180s harness passed. |
| `sandbox/ambient-denial` | PASS | PASS | PASS | PASS | Fixed host allowlist at `JintBehaviorScriptProvider.cs:931-1055`; ambient scans clean; 180s harness passed. |
| `sandbox/bounded-execution` | PASS | PASS | PASS | PASS | Hostile preflight and limit tests at `OmScriptingJintContractTests.cs:592-696`; 180s harness passed. |
| `sandbox/cancellation` | PASS | PASS | PASS | PASS | Cancellation tests at `OmScriptingJintContractTests.cs:2063-2125`; 180s harness passed. |
| `sandbox/error-mapping` | PASS | PASS | PASS | PASS | Error mapping code at `JintBehaviorScriptProvider.cs:1346-1462`; README lines `161-202`; 180s harness passed. |
| `sandbox/package-isolation` | PASS | PASS | PASS | PASS | Package isolation tests at `OmScriptingJintContractTests.cs:55-96`; package scan confirms only adapter pins Jint. |

## Totals

- Acceptance criteria checked: `44`
- Acceptance PASS: `41`
- Acceptance FAIL: `3`
- Behavior cases checked: `14`
- Behavior PASS: `14`
- Behavior FAIL: `0`
- Blocking issues: `2`
- Non-blocking issues: `0`
- Overall verdict: `FAIL`

## Conclusion

The implementation has strong structural and executable evidence for the Jint adapter semantics, and the complete OM harness can pass under a longer guarded run. However, the completed track fails the current verification because the required external 120-second watchdog harness timed out and the timeout did not identify a concrete test/wait point. This track should not be archived or accepted for mission node `G5-T2` until the 120-second watchdog requirement is restored and reverified.
