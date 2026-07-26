# Verification Report Rerun 001: add-dotnet-om-optional-script-runtime

Verified at: 2026-07-17T06:46:28Z

Verifier: fresh independent codument-verify executor for mission converge-dotnet-om-bun-capability-parity, node G5-T2.

## Blocking Issues

None.

## Non-Blocking Issues

None.

## Scope And Baseline

This was a read-only implementation/state review. The only file written by this verification is this report. I independently read track.xml, proposal, design, decisions, behavior delta, analysis/decision-tree.md, analysis/findings.md, mission G5 materials and parity decisions, plus every existing file in reports/. The prior verify-report.md was a failing baseline, not proof.

The previous report contains a 44-criterion baseline; the current track adds the three T4.3 criteria, for 47 current acceptance criteria and 14 behavior cases. The executable full harness includes OmScriptingJintContractTests.RunAsync through tests/Program.cs:7172-7175; the focused suite still directly enumerates all existing assertion groups at tests/OmScriptingJintContractTests.cs:17-52.

## Command Evidence

All .NET restore, build, source inspection, diagnostic run, and default harness runs used cwd /Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet and /usr/local/share/dotnet/dotnet (SDK 10.0.103, runtime 10.0.3, osx-arm64).

| Check | Result | Exact duration |
| --- | --- | ---: |
| dotnet restore tests/Cozo.DotNet.Om.Tests.csproj | exit 0; all projects up to date | 1.940s |
| MSBUILDTERMINALLOGGER=off dotnet build tests/Cozo.DotNet.Om.Tests.csproj --no-restore | exit 0; 0 warnings; 0 errors | 1.730s |
| Diagnostic external 120s watchdog with COZO_OM_TEST_DIAGNOSTICS=1 | exit 0; Cozo.DotNet OM tests passed. | 41.791s |
| Default external 120s watchdog run 1 | exit 0 | 41.793s |
| Default external 120s watchdog run 2 | exit 0 | 50.572s |
| Default external 120s watchdog run 3 | exit 0 | 44.628s |
| Default external 120s watchdog run 4 | exit 0 | 36.748s |
| Default external 120s watchdog run 5 | exit 0 | 53.459s |

Every run was a fresh process group supervised by external communicate(timeout=120), and the five default runs were serial. Each completed run had exact Cozo.DotNet.Om.Tests residual-process checks after 1 second and 3 seconds; all were zero. A final delayed exact-match check returned FINAL_RESIDUAL_DOTNET_TEST_PROCESS_COUNT=0. The match string was constructed at runtime, avoiding supervisor/shell self-match.

Diagnostic output is opt-in. It emitted START, WAIT, and COMPLETE records for every top-level matrix and Jint assertion group. The slowest observed top-level step was DEPA red-light detection at 12.555s; the slowest Jint group was Jint.AssertP2FiniteDefaultsBoundPendingPromisesAsync at 2.003s. Thus any future near-bound/timeout tail identifies its active step and elapsed duration without changing default-run output.

Structural checks:

- xmllint --noout passed for track.xml, the behavior delta, mission.xml, adapter project, and test project.
- codument validate add-dotnet-om-optional-script-runtime --strict passed from the workspace root. The CLI resolves a literal codument/ relative to its process cwd, so an attempted call from the mandated .NET cwd correctly reported not initialized; the workspace-root strict command is the required CLI-scoped structural check. No .NET verification command was moved from its mandated cwd.
- git diff --check passed for scoped tracked files. Separate git diff --no-index --check /dev/null scans for each relevant untracked file returned UNTRACKED_WHITESPACE_FAILURES=0.
- dotnet list packages/Om.Scripting.Jint/Cozo.DotNet.Om.Scripting.Jint.csproj package --include-transitive showed direct Jint 4.13.0 and transitive Acornima 1.6.2. The adapter project is the only direct Jint reference; tests reference only the adapter.
- Scoped production scans returned no AllowClr, .Result, .Wait(, or GetAwaiter().GetResult( in packages/Om.Scripting.Jint or src/Om.Core. Om.Core has no Jint/Acornima/JavaScript/script-runtime schema marker; existing query method parameters named script are not serialized script fields.

Direct source review confirms fresh engine creation per invocation, finite timeout/statements/recursion/memory/cancellation settings, awaited Promise unwrapping, and a frozen behavior-kind-aware host. Programmatic adapter contracts defensively copy immutable input/results; the focused executable test also reflects over canonical contracts to reject script fields.

## Rejudged Watchdog Evidence

| Criterion | Verdict | Independent evidence |
| --- | --- | --- |
| T3.4-AC1 | PASS | Five serial default watchdog exits are 0 in 36.748s-53.459s; each delayed and final residual check is zero. |
| T3.4-AC2 | PASS | Diagnostic watchdog exits 0 in 41.791s and emits active-step heartbeats; source review confirms five-second heartbeat plus Jint assertion labels. |
| T3.4-AC3 | PASS | This report records duration/status/cleanup; restore/build, XML, strict, and whitespace checks pass. |
| T4.1-AC1 | PASS | Clean build, six independently supervised full harness exits 0, XML and whitespace pass. |
| T4.3-AC1 | PASS | Diagnostic output names current test/wait and duration. |
| T4.3-AC2 | PASS | Five serial independent external 120s runs all exit 0 below threshold with no residual process. |
| T4.3-AC3 | PASS | RunAsync retains the direct full assertion list; diagnostics only announce and invoke/await existing assertions, with no filter, catch-and-continue, inner timeout, or changed assertion. |

The prior 120.025s timeout and empty output tail are superseded by reproducible current evidence. The threshold was not relaxed: five new default runs meet it, while diagnostic mode provides the formerly missing localization evidence.

## Acceptance Matrix

Exists, Substantive, and Wired are PASS for every row. Runtime semantics were executed by the restored, rebuilt, externally supervised full harness; package/schema/bridge rows also received the scoped source and dependency review above.

| Criterion | Verdict |
| --- | --- |
| T1.1-AC1 Jint isolated to adapter | PASS |
| T1.1-AC2 all slots map to typed collections | PASS |
| T1.1-AC3 conflict diagnostics before effects | PASS |
| T1.2-AC1 exact typed callbacks for import | PASS |
| T1.2-AC2 provider failure has no OM mutation | PASS |
| T1.2-AC3 immutable contracts/no Jint boundary leak | PASS |
| T1.3-AC1 RequireReady callbacks execute/no placeholder | PASS |
| T1.3-AC2 invalid inputs diagnose before effects | PASS |
| T1.3-AC3 reversed inputs keep deterministic order | PASS |
| T1.4-AC1 invalid/non-callable source fails preflight | PASS |
| T1.4-AC2 null/default graph returns diagnostics | PASS |
| T1.4-AC3 preflight does not execute function body | PASS |
| T2.1-AC1 all callback shapes convert typed results | PASS |
| T2.1-AC2 failures are stable and redacted | PASS |
| T2.1-AC3 fresh engine/no global leak | PASS |
| T2.2-AC1 loop/recursion/statement/memory limits bounded | PASS |
| T2.2-AC2 cancellation maps as cancellation | PASS |
| T2.2-AC3 no cross-invocation mutable state | PASS |
| T2.3-AC1 host allowlist denies ambient access | PASS |
| T2.3-AC2 async bridge has no sync wait | PASS |
| T2.3-AC3 parent action and transactional host writes | PASS |
| T2.4-AC1 finite defaults and pending Promise timeout | PASS |
| T2.4-AC2 no skipConstraints and redacted public errors | PASS |
| T2.4-AC3 stable resource classification/location | PASS |
| T2.5-AC1 callable preflight bounded | PASS |
| T2.5-AC2 hostile factory gives no-effect diagnostic | PASS |
| T2.5-AC3 legal function/arrow compatibility | PASS |
| T3.1-AC1 six ready callbacks/missing scripts atomic | PASS |
| T3.1-AC2 native/script coexistence and conflicts | PASS |
| T3.1-AC3 sandbox/resource/cancellation assertions | PASS |
| T3.2-AC1 parent/inheritance order matches C# runtime | PASS |
| T3.2-AC2 failure rolls back all effects | PASS |
| T3.2-AC3 cancellation prevents late effects | PASS |
| T3.3-AC1 docs state in-process, not OS sandbox | PASS |
| T3.3-AC2 programmatic source and script-free Om.Core schema | PASS |
| T3.4-AC1 five watchdog passes/no residual process | PASS |
| T3.4-AC2 timeout/hang localizable | PASS |
| T3.4-AC3 recorded timing/cleanup plus build/diff/XML | PASS |
| T4.1-AC1 build/harness/XML/diff | PASS |
| T4.1-AC2 adapter-only Jint/no synchronous bridge | PASS |
| T4.1-AC3 all behavior cases evidenced | PASS |
| T4.2-AC1 cross-slot reuse is OMS1002/zero bindings | PASS |
| T4.2-AC2 same-slot reuse remains compatible | PASS |
| T4.2-AC3 failure closes before effects and is documented | PASS |
| T4.3-AC1 diagnostics locate active test/wait | PASS |
| T4.3-AC2 five serial external 120s passes | PASS |
| T4.3-AC3 no weakened behavior/limits semantics | PASS |

## Behavior Case Matrix

| Behavior case | Exists | Substantive | Wired | Verdict |
| --- | --- | --- | --- | --- |
| binding/all-callback-shapes | PASS | PASS | PASS | PASS |
| binding/missing-script | PASS | PASS | PASS | PASS |
| binding/incompatible-reuse | PASS | PASS | PASS | PASS |
| binding/native-coexistence | PASS | PASS | PASS | PASS |
| execution/context-capabilities | PASS | PASS | PASS | PASS |
| execution/result-conversion | PASS | PASS | PASS | PASS |
| execution/parent-action-composition | PASS | PASS | PASS | PASS |
| execution/transaction-rollback | PASS | PASS | PASS | PASS |
| execution/fresh-engine | PASS | PASS | PASS | PASS |
| sandbox/ambient-denial | PASS | PASS | PASS | PASS |
| sandbox/bounded-execution | PASS | PASS | PASS | PASS |
| sandbox/cancellation | PASS | PASS | PASS | PASS |
| sandbox/error-mapping | PASS | PASS | PASS | PASS |
| sandbox/package-isolation | PASS | PASS | PASS | PASS |

## Summary

- Acceptance criteria: 47 / 47 PASS; 0 FAIL (the former 44-row baseline plus T4.3's three reopened criteria).
- Behavior cases: 14 / 14 PASS; 0 FAIL.
- Blocking issues: 0. Non-blocking issues: 0.
- Verdict: PASS. The completed track has current independent executable and structural evidence for G5-T2; the old watchdog failure has been replaced by verified evidence, not merely described away.
