# Harness Diagnosis 001

Date: 2026-07-17

## Diagnostic Switch

The complete OM harness is silent by default. Set `COZO_OM_TEST_DIAGNOSTICS=1`
to emit progress records to stderr:

```sh
COZO_OM_TEST_DIAGNOSTICS=1 /usr/local/share/dotnet/dotnet run \
  --project tests/Cozo.DotNet.Om.Tests.csproj --no-build
```

`Program.cs` records the active top-level matrix and emits a five-second
`WAIT` heartbeat with the active step duration and total elapsed time. The
Jint contract suite records every existing assertion group through the same
mechanism. Therefore, a watchdog output tail identifies both the last active
test or wait point and its duration without adding normal-run noise.

## Diagnosis

The historical verifier result was a 120-second timeout with no test output,
followed by one 180-second pass at 122.866 seconds. After adding the opt-in
diagnostics, a fresh externally supervised 120-second run completed in
91.682 seconds. Its slowest concrete top-level step was `DEPA red-light
detection` at 38.880 seconds. The next slowest recorded steps were `DEPA
report aggregation` at 11.777 seconds and `DEPA observation and scan` at
11.639 seconds.

The Jint matrix was not the slow path. Its slowest individual assertion group
was `AssertP2FiniteDefaultsBoundPendingPromisesAsync` at 2.004 seconds; all
other Jint groups completed below 1.4 seconds. No currently reproducible
test or wait point exceeded the 120-second external limit, so there is no
evidence-supported production performance change to make. The implemented
repair is diagnostic observability: a future timeout now leaves a specific
active step and continuously updated duration instead of an empty output
tail.

Excluded explanations:

- No test, callback shape, resource-limit probe, or behavior case was skipped,
  removed, suppressed, or weakened.
- No Jint package or OM runtime behavior was changed to improve timing.
- The historical timeout is not attributed to generic machine slowness; the
  current diagnostic and five independent watchdog runs provide the concrete
  timing evidence available in this environment.

## Build Evidence

From `cozo-lib-dotnet`:

```sh
MSBUILDTERMINALLOGGER=off /usr/local/share/dotnet/dotnet build \
  tests/Cozo.DotNet.Om.Tests.csproj --no-restore
```

Result: exit `0`, `0` warnings, `0` errors.

## External Watchdog Evidence

Each independent run used a process-level 120-second alarm, not an in-process
test timeout:

```sh
/usr/bin/time -lp perl -e 'alarm shift; exec @ARGV' 120 \
  /usr/local/share/dotnet/dotnet run \
  --project tests/Cozo.DotNet.Om.Tests.csproj --no-build
```

After every run, this residual check produced no output:

```sh
ps -axo pid,ppid,pgid,etime,command | \
  rg 'Cozo\.DotNet\.Om\.Tests|dotnet run --project tests/Cozo\.DotNet\.Om\.Tests' || true
```

| Run | Exit | Duration | Output | Residual check |
| --- | --- | ---: | --- | --- |
| 1 | `0` | 52.34s | `Cozo.DotNet OM tests passed.` | no matching process |
| 2 | `0` | 53.52s | `Cozo.DotNet OM tests passed.` | no matching process |
| 3 | `0` | 48.85s | `Cozo.DotNet OM tests passed.` | no matching process |
| 4 | `0` | 48.97s | `Cozo.DotNet OM tests passed.` | no matching process |
| 5 | `0` | 47.19s | `Cozo.DotNet OM tests passed.` | no matching process |

All five runs remained below the external 120-second threshold.
