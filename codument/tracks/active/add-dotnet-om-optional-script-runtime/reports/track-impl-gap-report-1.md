# Gap Loop Round 1 Report: add-dotnet-om-optional-script-runtime

Scope: phase P4 (final track convergence)

Round metadata: `track.xml` already records `<gap-round>1</gap-round>`.

Verdict: NO_GAP

## Method

This round independently reread the track truth surface:

- `proposal.md`
- `design.md`
- `decisions.md`
- `behavior_deltas/cozo-dotnet-om/delta.xml`
- `track.xml`
- existing `reports/final-verification.md`
- current implementation and all uncommitted files in the checkout

Prior verification was treated as evidence only. Each behavior case, P4
acceptance criterion, and hidden adversarial probe was mapped back to current
source, tests, docs, and fresh command results.

## Fresh Verification Results

Commands were run with process-level bounds for runtime execution.

### Build

Command:

```sh
/usr/local/share/dotnet/dotnet restore tests/Cozo.DotNet.Om.Tests.csproj
MSBUILDTERMINALLOGGER=off /usr/local/share/dotnet/dotnet build \
  tests/Cozo.DotNet.Om.Tests.csproj --no-restore
```

Result:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Full OM Harness

The full harness was run through a Python process supervisor using
`start_new_session=True`, `communicate(timeout=180)`, and process-group
termination on timeout.

Command under supervisor:

```sh
/usr/local/share/dotnet/dotnet run \
  --project tests/Cozo.DotNet.Om.Tests.csproj --no-build
```

Result:

```text
Cozo.DotNet OM tests passed.
WATCHDOG_TIMEOUT=0
EXIT_CODE=0
DURATION_SECONDS=55.862
WATCHDOG_RESIDUAL_GROUP=0
```

### XML, Codument, And Whitespace

Commands:

```sh
xmllint --noout \
  codument/tracks/add-dotnet-om-optional-script-runtime/track.xml \
  codument/tracks/add-dotnet-om-optional-script-runtime/behavior_deltas/cozo-dotnet-om/delta.xml \
  cozo-lib-dotnet/packages/Om.Scripting.Jint/Cozo.DotNet.Om.Scripting.Jint.csproj \
  cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj
codument validate add-dotnet-om-optional-script-runtime --strict
git diff --check -- <tracked files touched by the .NET OM/Jint work>
git diff --no-index --check -- /dev/null <untracked adapter/test/track files>
```

Result:

```text
add-dotnet-om-optional-script-runtime: track.xml OK + 1 behavior delta(s)
```

No whitespace failures were reported for tracked or relevant untracked files.

### Dependency Isolation

Command:

```sh
/usr/local/share/dotnet/dotnet list \
  cozo-lib-dotnet/packages/Om.Scripting.Jint/Cozo.DotNet.Om.Scripting.Jint.csproj \
  package --include-transitive
```

Result:

```text
Top-level Package      Requested   Resolved
> Jint                 4.13.0      4.13.0

Transitive Package      Resolved
> Acornima              1.6.2
```

Focused source scans confirmed `Jint` is directly referenced only by
`cozo-lib-dotnet/packages/Om.Scripting.Jint/Cozo.DotNet.Om.Scripting.Jint.csproj`,
with the test project referencing the adapter project and no `Om.Core` engine
dependency or script schema.

## Acceptance Mapping

### T4.1-AC1: build, harness, XML, diff

Status: PASS

Evidence:

- Build exited 0 with no warnings or errors.
- Full OM harness exited 0 under a 180 second watchdog.
- `xmllint` and `codument validate --strict` passed.
- Tracked and relevant untracked whitespace checks passed.

### T4.1-AC2: dependency and host boundary

Status: PASS

Evidence:

- Adapter project pins Jint 4.13.0.
- Root `Cozo.DotNet.csproj` and `src/Om.Core` are engine-free.
- Provider uses bounded fresh engines, Task interop only for invocation, no
  `AllowClr`, and no module/filesystem/network host surface.
- Focused provider scan found no synchronous `.Result`, `.Wait(`, or
  `.GetAwaiter().GetResult(` host bridge.

### T4.1-AC3: all behavior delta cases covered

Status: PASS

Evidence:

- `OmScriptingJintContractTests.RunAsync()` is wired into the full harness at
  `cozo-lib-dotnet/tests/Program.cs:7140`.
- The suite covers all callback shapes, exact binding ids, missing scripts,
  incompatible reuse, native/script coexistence, host capability boundaries,
  result conversion, parent-action composition, rollback, fresh-engine
  isolation, ambient denial, bounded execution, cancellation, error mapping,
  and package isolation.

### T4.2: cross-slot same-id reuse convergence

Status: PASS

Evidence:

- Provider rejects incompatible shape reuse and distinct-slot reuse before
  callable preflight or import effects.
- Tests prove constraint When/Then same-id cross-slot reuse returns `OMS1002`
  and zero bindings, while same-id/same-slot action reuse remains compatible.
- README documents the cross-slot restriction and native/script same-id conflict.

## Adversarial Probe Matrix

| Probe | Result | Evidence |
| --- | --- | --- |
| False readiness / partial bindings | PASS | Missing, syntax-invalid, non-callable, conflict, and `RequireReady` paths remain unresolved or atomically fail before effects. |
| Same-id shape / slot reuse | PASS | Distinct shapes and distinct slots return deterministic `OMS1002`; same slot compatible reuse is tested. |
| Callable preflight resource bypass | PASS | Factory expressions are evaluated under timeout, statements, recursion, memory, and cancellation limits with outer guards. |
| Promise timeout / cancellation | PASS | Pending promises time out; caller cancellation remains `OperationCanceledException`; full harness is externally bounded. |
| Host CLR/raw-context/option escape | PASS | Host is frozen, prototype-null, kind-aware, and denies CLR/System/require/importModule/raw runtime plus `skipConstraints`. |
| Source / secret leakage | PASS | Public diagnostics and exceptions are source-redacted; README warns source/secret loading policy stays outside the package. |
| Result conversion edge cases | PASS | Non-JSON, Date, function, Map, bad mutation specs, and wrong typed results map to stable conversion failures. |
| Native/script coexistence/restart | PASS | Native and script callbacks import together; same-id conflicts fail; persistent restart requires exact rebind without persisted sources. |
| Parent action / nearest mutation / interceptor order | PASS | P3 composition test verifies parent return is not pre-applied, child nearest mutation wins, and interceptor order matches runtime semantics. |
| Property+relation rollback and late effects | PASS | Script throw, host failure, returned mutation failure, after-interceptor failure, and cancellation roll back properties and relations; late effects are checked after delay. |
| Dependency/schema isolation | PASS | Jint is confined to the adapter; canonical manifest/runtime schema remains script-source-free. |
| Docs accuracy | PASS | README covers callback signatures, allowlisted host, limits/cancellation, diagnostics, import/restart/transactions, and in-process non-sandbox boundary. |

## Behavior Delta Mapping

| Behavior case | Verdict |
| --- | --- |
| `binding/all-callback-shapes` | PASS |
| `binding/missing-script` | PASS |
| `binding/incompatible-reuse` | PASS |
| `binding/native-coexistence` | PASS |
| `execution/context-capabilities` | PASS |
| `execution/result-conversion` | PASS |
| `execution/parent-action-composition` | PASS |
| `execution/transaction-rollback` | PASS |
| `execution/fresh-engine` | PASS |
| `sandbox/ambient-denial` | PASS |
| `sandbox/bounded-execution` | PASS |
| `sandbox/cancellation` | PASS |
| `sandbox/error-mapping` | PASS |
| `sandbox/package-isolation` | PASS |

## Conclusion

No implementation, design, behavior delta, or track XML gap was found for P4.
This round made no changes outside this report.
