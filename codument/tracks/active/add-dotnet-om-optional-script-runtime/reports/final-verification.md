# Final Verification: add-dotnet-om-optional-script-runtime

Verified at: `2026-07-17T05:10:52Z`

Scope: Codument track leaf `T4.1`. Verification was read-only except for this
report.

## Blocking Issues

None.

## Non-Blocking Issues

None.

## Summary

- Verdict: **PASS**
- Track acceptance criteria checked: **3 / 3 PASS**
- Behavior requirements checked: **1 / 1 PASS**
- Behavior cases checked: **14 / 14 PASS**
- Uncovered behavior cases: **0**
- Build: **PASS**, `0 Warning(s)`, `0 Error(s)`
- Full OM harness: **PASS**, exit `0` in `58.088s`
- Watchdog timeout: `0`; residual process group: `0`; final matching test
  process count: `0`
- Dependency, capability, schema, XML, Codument strict validation, documentation,
  and whitespace checks: **PASS**

## Command Evidence

### Restore And Build

Working directory: `cozo-lib-dotnet`

```sh
/usr/local/share/dotnet/dotnet restore tests/Cozo.DotNet.Om.Tests.csproj
MSBUILDTERMINALLOGGER=off /usr/local/share/dotnet/dotnet build \
  tests/Cozo.DotNet.Om.Tests.csproj --no-restore
```

Outcome:

```text
All projects are up-to-date for restore.
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:01.62
```

### Full Harness With External Watchdog

The test process was launched by a Python process supervisor with
`start_new_session=True`, `communicate(timeout=120)`, process-group termination
on timeout, and a final process-group liveness check:

```sh
/usr/local/share/dotnet/dotnet run \
  --project tests/Cozo.DotNet.Om.Tests.csproj --no-build
```

Outcome:

```text
Cozo.DotNet OM tests passed.
WATCHDOG_TIMEOUT=0
EXIT_CODE=0
DURATION_SECONDS=58.088
WATCHDOG_RESIDUAL_GROUP=0
RESIDUAL_DOTNET_TEST_PROCESS_COUNT=0
```

The Jint contract suite is wired into the full harness at
`cozo-lib-dotnet/tests/Program.cs:7140`. Its `RunAsync` entry invokes every
focused test listed below at
`cozo-lib-dotnet/tests/OmScriptingJintContractTests.cs:17-51`.

### Dependency Isolation

```sh
rg -n -i 'Jint|Acornima|Om\.Scripting\.Jint' cozo-lib-dotnet \
  --glob '*.csproj' --glob 'Directory.*' --glob '*.props' \
  --glob '*.targets' --glob '!**/obj/**' --glob '!**/bin/**'
/usr/local/share/dotnet/dotnet list \
  cozo-lib-dotnet/packages/Om.Scripting.Jint/Cozo.DotNet.Om.Scripting.Jint.csproj \
  package --include-transitive
```

Outcome:

- The only direct `Jint` `PackageReference` is
  `packages/Om.Scripting.Jint/Cozo.DotNet.Om.Scripting.Jint.csproj:17`.
- The reference is pinned to requested and resolved version `4.13.0`.
- `Acornima 1.6.2` is transitive through Jint, not directly referenced.
- The test project references the optional adapter project at
  `tests/Cozo.DotNet.Om.Tests.csproj:13`; it does not reference Jint directly.
- The root project and `src/Om.Core` have no Jint or Acornima reference.
- The executable isolation assertion is at
  `tests/OmScriptingJintContractTests.cs:54-96`.

### Host, Capability, And Schema Boundary

Production-source scans produced:

```text
PRODUCTION_SYNC_BRIDGE_FILE_COUNT=0
PRODUCTION_AMBIENT_CAPABILITY_FILE_COUNT=0
PUBLIC_RAW_RUNTIME_EXPOSURE_FILE_COUNT=0
ROOT_OMCORE_ENGINE_REFERENCE_FILE_COUNT=0
OMCORE_SCRIPT_SCHEMA_FIELD_FILE_COUNT=0
```

The scans covered:

```text
.Result
.Wait(
GetAwaiter().GetResult(
AllowClr
module-loader APIs
filesystem/network APIs
public raw runtime exposure
Jint/Acornima in root or Om.Core
scriptSource/sourceName/scriptRuntime fields in Om.Core
```

Structural evidence:

- `CreateEngine` applies timeout, statements, recursion, memory, cancellation,
  and optional TaskInterop without enabling CLR or modules:
  `JintBehaviorScriptProvider.cs:873-907`.
- `CreateHost` builds the fixed callback-context allowlist:
  `JintBehaviorScriptProvider.cs:916-1030`.
- Canonical JSON is inspected at runtime to reject source, source name, Jint,
  and script-runtime fields while retaining provider-neutral binding metadata:
  `OmScriptingJintContractTests.cs:2239-2258`.
- Om.Core reflection and source checks reject engine dependencies and script
  schema additions: `OmScriptingJintContractTests.cs:82-96`.

### XML And Codument Validation

```sh
xmllint --noout \
  codument/tracks/add-dotnet-om-optional-script-runtime/track.xml \
  codument/tracks/add-dotnet-om-optional-script-runtime/behavior_deltas/cozo-dotnet-om/delta.xml \
  cozo-lib-dotnet/packages/Om.Scripting.Jint/Cozo.DotNet.Om.Scripting.Jint.csproj \
  cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj
codument validate add-dotnet-om-optional-script-runtime --strict
```

Outcome:

```text
XML_EXIT=0
add-dotnet-om-optional-script-runtime: track.xml OK + 1 behavior delta(s)
CODUMENT_VALIDATE_EXIT=0
TASKSPACE_COUNT=1
TASK_COUNT=14
BEHAVIOR_REQUIREMENT_COUNT=1
BEHAVIOR_SUITE_COUNT=3
BEHAVIOR_CASE_COUNT=14
UNIQUE_BEHAVIOR_IDS=True
```

The Codument CLI was available, so no fallback-only verdict was needed.

### Diff And Whitespace

```sh
git diff --check -- \
  cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj \
  cozo-lib-dotnet/tests/Program.cs
git diff --no-index --check -- /dev/null <each relevant untracked file>
```

Outcome:

```text
TRACKED_DIFF_CHECK_EXIT=0
UNTRACKED_WHITESPACE_FAILURES=0
```

The untracked check covered the adapter package, focused test file, and this
track's files.

## Goal-Backward Verification

| Criterion | Exists | Substantive | Wired | Verdict |
| --- | --- | --- | --- | --- |
| `T4.1-AC1`: build, harness, XML, diff | Adapter/tests/track files exist | Build and harness exit 0; XML and whitespace checks pass | Test project references adapter and `Program.cs` runs the suite | **PASS** |
| `T4.1-AC2`: dependency and host boundary | Separate optional package exists with Jint 4.13.0 | Root/Om.Core engine scans and production sync-bridge scans are zero | Full harness executes async host and sandbox probes | **PASS** |
| `T4.1-AC3`: all behavior cases evidenced | One requirement, three suites, fourteen unique cases exist | Every case maps to an executable test or exact structural assertion | All mapped tests run through the full harness entry | **PASS** |

## Behavior Case Matrix

### Suite `binding`

| Case | Executable or structural evidence | Verdict |
| --- | --- | --- |
| `all-callback-shapes` | Exact six-collection slot mapping at `OmScriptingJintContractTests.cs:143-163`; strict canonical import and execution at `:1387-1452` | **PASS** |
| `missing-script` | Stable `OMS1001` and absent binding at `:166-183`; unresolved/atomic `RequireReady` behavior at `:1454-1523` | **PASS** |
| `incompatible-reuse` | Action/interceptor same-ID reuse rejected as `OMS1002` before bindings at `:185-195` | **PASS** |
| `native-coexistence` | Distinct native/script IDs coexist and same-ID conflict yields `OMS1004` at `:210-235`; canonical execution at `:1387-1452` | **PASS** |

### Suite `execution`

| Case | Executable or structural evidence | Verdict |
| --- | --- | --- |
| `context-capabilities` | Allowed current/as-of/neighbor reads and denial matrix at `:714-808`; write/link/parent capabilities at `:810-921`; transactional integration at `:1717-1843` | **PASS** |
| `result-conversion` | All typed callback result shapes at `:663-712`; invalid JSON/typed result matrix at `:1042-1146` | **PASS** |
| `parent-action-composition` | Parent mutations returned without pre-application at `:810-921`; child composition and nearest mutation at `:1717-1843` | **PASS** |
| `transaction-rollback` | Script, host, returned-mutation, and after-interceptor rollback matrix at `:1845-1958`; cancellation and no-late-effect proof at `:1960-2029` | **PASS** |
| `fresh-engine` | Concurrent global-state isolation across 32 invocations at `:1353-1370`; `InvokeScriptAsync` creates an engine per call at `JintBehaviorScriptProvider.cs:832-859` | **PASS** |

### Suite `sandbox`

| Case | Executable or structural evidence | Verdict |
| --- | --- | --- |
| `ambient-denial` | Host and CLR/ambient denial at `OmScriptingJintContractTests.cs:714-808`; ready-import CLR/System/require/import denial at `:1601-1623`; production capability scans are zero | **PASS** |
| `bounded-execution` | Hostile preflight factories at `:489-593`; finite defaults and pending Promise timeout at `:1194-1221`; timeout/statements/recursion/memory mapping at `:1223-1282` and ready import at `:1601-1696` | **PASS** |
| `cancellation` | Host cancellation with no late write at `:984-1040`; pre/during execution cancellation at `:1313-1351`; transaction cancellation rollback at `:1960-2029` | **PASS** |
| `error-mapping` | Compile preflight identity/location at `:400-452`; host failure/redaction at `:924-982`; result and runtime failure matrices at `:1042-1192`; structured exception fields at `JintBehaviorScriptException.cs:30-104` | **PASS** |
| `package-isolation` | Executable project/reflection/schema audit at `OmScriptingJintContractTests.cs:54-96`; canonical payload audit at `:2239-2258`; package and source scans above | **PASS** |

## Documentation Verification

`packages/Om.Scripting.Jint/README.md` covers:

- callback signatures and typed JSON results: `JavaScript Callback Contract`
- behavior-kind capability matrix: `Allowlisted OM Host`
- finite limits, cancellation, preflight, and fresh-engine lifetime:
  `Limits, Cancellation, And Lifetime`
- stable diagnostics and errors: `Diagnostics And Exceptions`
- canonical import, restart rebind, and transaction semantics:
  `Import, Restart, And Transactions`
- hostile-code non-goal and in-process defense-in-depth boundary:
  `Security Boundary`

All eight public adapter types have XML documentation, including source
definition, options, request/result/diagnostic contracts, provider, failure
phase, and structured exception.

## Final Verdict

**PASS.** `T4.1` has complete executable and structural evidence. No behavior
case is uncovered, and no verification failure requires a return to
implementation.
