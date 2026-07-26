# Findings

## Planning Baseline

- G4 supplies immutable behavior catalogs, canonical JSON, exact callback binding ids, `BehaviorCallbackBindingSet`, unresolved readiness and atomic `RequireReady` import.
- Existing C# callback contracts cover conditional/custom constraints, computed properties, actions, mutations and interceptors with async context APIs.
- Bun callbacks can read and write through behavior context, return mutation specifications and call a parent action. The Jint adapter must map those observable capabilities through an explicit host façade rather than exposing the C# runtime.
- `Om.Core` currently has no Jint dependency. Existing optional YAML functionality establishes the package pattern under `cozo-lib-dotnet/packages/`.
- The official NuGet gallery exposes Jint 4.13.0 for modern .NET as of 2026-07-17; the package will be pinned rather than floated.

## Scope Guard

- No script persistence, script discovery service, filesystem module loader, network fetch, package manager or generic CLR interop.
- No changes to canonical behavior manifest schema are required: binding ids remain the join key.
- No replacement of typed C# callbacks. The script provider is one optional producer of those callbacks.
- No claim of OS/process sandboxing for hostile code. Jint limits are in-process defense in depth; unsupported memory-limit configuration must be rejected before execution.

## T1.1 Spot Check

- Added a focused executable contract harness and the future adapter project reference without production implementation.
- Tests lock immutable definitions/options/results, ordinal binding ids, all six current catalog kind/slot mappings, missing/duplicate/incompatible/native conflicts and effect-free provider validation.
- Parent red verification used `/usr/local/share/dotnet/dotnet`: restore skipped only the absent adapter project, and build failed only on the absent project/namespace/types. `git diff --check` passed.

## T1.2 Spot Check

- Added the isolated net10.0 adapter package with Jint 4.13.0 pinned and no root/Om.Core reference.
- Provider contracts defensively copy inputs/results, map exact ordinal binding ids to all six typed callback collections and return stable missing/incompatible/duplicate/native-conflict diagnostics before effects.
- Parent verification restored the new package, built with zero warnings/errors and passed the complete OM harness. P1 attractor must still resolve whether placeholder delegates are an acceptable phase-local red/green boundary or create false readiness.

## P1 Coding Attractor Round 1

- Result: GAP. Generated placeholder delegates satisfy type identity but G4 import projects them as `Ready`, while every invocation throws. This is false readiness and violates the honest vertical-slice meaning of T1.2-AC1.
- Direct provider inputs also silently skip invalid kind/slot and blank callback ids, accept invalid definition/options values, and emit some diagnostics/bindings in caller order.
- Controlled revision adds T1.3: implement minimal real pure-script invocation for all generated delegate shapes, structured direct-input validation and normalized output ordering. P2 remains responsible for bounded limits and async OM host capabilities.

## T1.3 Spot Check

- Removed placeholder delegates. Every generated callback now creates a fresh Jint engine, invokes the supplied callable and converts the six typed results; focused tests perform RequireReady import and execute all callback collections.
- Added fatal structured diagnostics for invalid definition ids/sources, blank catalog binding ids, invalid kind/slot and non-positive options. Fatal preflight returns no bindings and has no OM effects.
- Diagnostics and each typed binding collection are normalized independently of caller order. Parent build completed with zero warnings/errors, the full OM harness passed and no `.Result`/`.Wait()` bridge exists.

## P1 Coding Attractor Round 2

- Result: GAP. Valid sources now execute, but syntax-invalid or non-callable sources are not evaluated until first invocation; RequireReady can therefore still publish them as ready.
- Public record copy/init state and null elements can also bypass constructor normalization and produce raw null/default failures before structured diagnostics.
- Controlled revision adds T1.4: preflight every referenced source as callable in a fresh no-host engine and normalize the complete request graph before all LINQ/dictionary operations. Limit overflow and raw context projection remain explicitly assigned to P2.

## T1.4 Spot Check

- Referenced source is now evaluated in a fresh no-host engine only to prove it yields a callable; syntax/evaluation and non-callable failures return fatal binding-aware diagnostics with no source text and zero bindings.
- The function body is not invoked by preflight. Tests prove a body that throws is accepted by BuildBindings and throws only on callback invocation.
- Null/default request members, definitions/catalog/native collections and null elements are normalized before dereference or LINQ. Parent build passed with zero warnings/errors, full harness passed and no blocking Task bridge exists.

## P1 Coding Attractor Round 3

- Result: PASS. Fresh review confirmed exact slot mapping, package isolation, honest executable readiness, callable preflight, complete request-graph diagnostics and deterministic output ordering.
- Full OM harness passed from the required `cozo-lib-dotnet` working directory. Finite-limit overflow, JSON-only host projection and detailed runtime errors remain correctly assigned to P2.

## T2.1 RED Spot Check

- Added executable contracts for all six callback result shapes, invalid JSON/result matrices, stable binding-aware runtime/conversion/limit failures, source redaction, cancellation semantics, fresh-engine concurrency and the absence of synchronous Task bridges.
- Parent RED verification ran the test project build from `cozo-lib-dotnet`. It failed only because the planned public `JintBehaviorScriptFailurePhase` and `JintBehaviorScriptException` contracts do not exist yet: three compiler errors, zero warnings.
- The RED surface is therefore isolated to T2.2 production work; it does not indicate an unrelated package, fixture or existing OM regression.

## T2.2 Spot Check

- Added the public structured script exception model and stable execution, conversion, timeout and resource-limit codes while retaining the original exception without exposing full source text.
- Each callback invocation now creates and owns a fresh bounded Jint engine. Caller cancellation remains `OperationCanceledException`; timeout, statement, recursion and memory failures retain distinct binding-aware adapter identities.
- Result conversion is explicitly JSON-shaped and rejects non-finite numbers, functions, `Date`, `Map`, cyclic/non-plain objects and malformed mutation specifications.
- Parent verification from `cozo-lib-dotnet` built with zero warnings/errors and passed the complete OM harness. The production adapter has no `.Result`, `.Wait()` or `GetAwaiter().GetResult()` bridge.

## T2.3 Spot Check

- Added a behavior-kind-aware Jint-native host façade for current/as-of property reads, neighbor reads, writes, links and action-only parent dispatch. Identity and parameters are JSON-projected and deeply frozen; host members are closure-wrapped and the raw callbacks/context/runtime are not script-visible.
- Async host work forwards the invocation cancellation token. Parent action returns JSON mutation specifications without applying them, while writes and links remain routed through existing OM callback contexts and their transaction runtime.
- Host failures map to source-redacted `OMS2004` / `HostInvocation`; caller cancellation remains cancellation. Tests cover kind-specific denial, CLR/module/ambient denial, Promise behavior, computed AsOf reads, mutation/action/interceptor effects, parent composition and cancellation rollback without late writes.
- Parent build completed with zero warnings/errors, the complete OM harness passed, XML/diff checks passed and the only synchronous-wait scan hits were the tests' own negative assertions.

## P2 Coding Attractor Round 1

- Result: GAP. Defaults remain unbounded even though the design requires finite defaults, and timeout does not cover a pending Promise's full wait because only the caller token reaches `UnwrapIfPromiseAsync`.
- The host write-options projection exposes `skipConstraints`, which was not granted by the allowlist design. Runtime script error details can also disclose a thrown secret because only an exact full-source substring is redacted.
- The timeout test races its statement limit and was observed failing once as `OMS2102` before passing on retry. Compile/runtime location identity is declared but not populated or asserted.
- Controlled revision adds T2.4 to enforce finite defaults and complete timeout, remove the constraint-bypass capability, make public errors secret-safe, stabilize resource probes and add real source-location evidence.

## T2.4 Spot Check

- Effective options now normalize unset values to finite defaults: two seconds, 250,000 statements, recursion depth 128 and 32 MiB. The linked timeout/caller token covers Promise unwrapping, including a Promise that never settles.
- Script write options allow only `validTime`; `skipConstraints` and unknown options become structured host failures. Public execution details are fixed and no longer echo arbitrary JavaScript error text, while the original failure remains the inner exception.
- Compile diagnostics and runtime exceptions now carry source name, phase and positive line/column when Jint or Acornima provides a location.
- Parent build completed with zero warnings/errors and the complete OM harness passed three consecutive runs. Safety, XML and diff checks also passed.

## P2 Coding Attractor Round 2

- Result: GAP. All round-1 findings are closed, but callable preflight still evaluates the source expression in a bare `new Engine()` before invocation limits apply.
- A factory expression such as `(() => { while (true) {} return () => true; })()` can therefore block `BuildBindings` indefinitely despite finite callback defaults.
- Controlled revision adds T2.5 to apply the same finite timeout, statement, recursion, memory and cancellation policy during callable preflight and to prove a hostile factory expression fails without hanging or returning bindings.

## T2.5 Spot Check

- Callable preflight now uses the shared finite `CreateEngine` path with Task interop disabled, applying timeout, statements, recursion, memory and caller cancellation before source evaluation.
- Four hostile factory probes cover timeout, statement, recursion and memory exhaustion under an outer three-second guard. They return source-redacted `OMS1013` diagnostics, zero bindings and no OM effects; pre-cancelled callers retain `OperationCanceledException`.
- Parent build completed with zero warnings/errors and the complete OM harness passed two consecutive runs. No bare engine construction, synchronous Task bridge, `AllowClr`, XML or diff issue remains in the checked scope.

## P2 Coding Attractor Round 3

- Result: PASS. Fresh review found no blocking or non-blocking issue across bounded preflight, finite invocation defaults, typed conversion, error redaction/location, fresh-engine isolation, host capability boundaries, async bridging, parent composition and cancellation rollback.
- Independent build, complete OM harness, diff and XML checks passed. A temporary out-of-tree probe was discarded because its external project reference could not resolve; repository executable tests cover the same adversarial preflight cases.

## T3.1 Spot Check

- Canonical JSON now drives a strict ready import covering script constraints, validator, computed, action, mutation and interceptor alongside a native computed callback. Exact binding ids survive encode/decode and every callback executes through the imported OM registry.
- Missing scripts and native/script same-id conflicts remain unresolved and atomically effect-free under `RequireReady`. A provider integration defect was fixed so a catalog slot satisfied by a matching native callback is not falsely diagnosed as missing script, while native+script conflicts still return `OMS1004`.
- SQLite restart tests prove behavior metadata persists but process-local native/script callbacks and sources do not; readiness becomes unresolved until the exact callbacks are supplied again.
- Parent build completed with zero warnings/errors and the full harness passed twice. Ambient denial, resource/cancellation identity, package isolation, XML and diff checks passed.

## T3.2 Spot Check

- Canonical ready imports now exercise base/child script action overrides, explicit parent mutation-spec composition, nearest child mutation resolution and deterministic inherited before/action/mutation/after ordering.
- Direct host property/link effects and returned mutations commit in one existing `ExecuteActionAsync` transaction. Parent action state is observed before mutation application, proving `callParentAction` returns specs only.
- Script throw, host failure, returned mutation failure and after-interceptor failure each roll back property, relation, mutation and interceptor effects. Cancellation during blocked async host work stays `OperationCanceledException`, unwinds the transaction and produces no delayed effects.
- No production change was required. Parent build completed with zero warnings/errors and the full harness passed twice; safety, XML and diff checks passed.

## T3.3 Spot Check

- Added a package README documenting the canonical metadata plus programmatic source flow, exact callback signatures/results, identity/host capability matrix, valid-time-only writes, limits, diagnostics, readiness/restart and transaction semantics.
- Public provider and exception contracts now have focused XML documentation for IntelliSense.
- The security section explicitly describes in-process defense in depth, not OS/process sandboxing, and forbids treating the absence of CLR/module/filesystem/network APIs as hostile-code isolation.
- Parent build and complete harness passed; documentation/boundary and diff checks confirm Jint and script schema remain outside `Om.Core` and the root project.

## P3 Coding Attractor Round 1

- Result: GAP. The fresh reviewer reported its complete harness did not return for approximately 1,382 seconds and required terminating matching `dotnet` processes.
- Parent and leaf-worker runs had repeatedly completed, so the evidence does not yet isolate a deterministic product/test defect; however, an unbounded independent verification cannot be accepted as PASS.
- Controlled revision adds T3.4 to run repeated process-guarded harnesses, record durations and isolate any hanging P3 test before another attractor attempt.

## T3.4 Spot Check

- Five fresh-worker harness runs completed under independent 120-second process watchdogs in 47, 45, 61, 51 and 43 seconds, all exit 0 with no timeout or residual test process.
- Parent repeated the process-guarded harness successfully in 44 seconds with exit 0 and no residual process. An earlier wrapper-only exit 1 was caused after a passing harness by assigning zsh's read-only `status` variable; rerunning with `rc` closed the wrapper cleanly.
- The previous 1,382-second reviewer hang is not reproducible and is classified as verifier/probe behavior rather than a demonstrated adapter or test defect. No code edit was needed.

## P3 Coding Attractor Round 2

- Result: PASS with no blocking or non-blocking issue.
- Fresh review built with zero warnings/errors and ran the full harness five more times under 120-second process watchdogs in 50.571, 46.909, 47.176, 60.468 and 46.956 seconds; every run exited 0 with no residual process.
- Canonical readiness/restart, safety limits, inherited behavior ordering, transaction rollback, cancellation/no-late-effect, documentation and package/schema boundaries all have executable or structural evidence.

## T4.1 Spot Check

- Fresh final verification restored and built with zero warnings/errors, completed the watchdog harness in 58.088 seconds with no residual process, and passed Codument strict/XML/diff/security/dependency checks.
- The behavior delta contains one requirement, three suites and fourteen unique cases; all 14 map to executable tests or exact structural evidence with no uncovered case.
- The verification report records command outcomes, package isolation, public documentation coverage and a goal-backward 3/3 acceptance matrix. Result: PASS.

## P4 Coding Attractor Round 1

- Result: GAP. Reusing one binding id for constraint `when` and `then` produces one shared typed delegate that captures only the first sorted reference, so a failure in the other slot reports the wrong `JintBehaviorScriptException.Slot`.
- The design allows compatible reuse only when slot identity remains accurate and no duplicate typed binding is required. The current provider-neutral collection cannot satisfy that condition for distinct constraint slots.
- Controlled revision adds T4.2 to reject same-id cross-slot reuse deterministically as `OMS1002`, return zero bindings/effects, add executable coverage and document the restriction.

## T4.2 Spot Check

- Provider validation now distinguishes incompatible callback shapes from distinct-slot identity ambiguity. Both return deterministic `OMS1002` before preflight or binding construction.
- Canonical strict-import tests prove same-id `when`/`then` reuse yields zero bindings and zero OM effects, while same-id/same-shape/same-slot reuse produces one typed callback that satisfies and executes for two action entries.
- README documents the restriction. Parent build completed with zero warnings/errors, the watchdog harness exited 0 in 74 seconds, and root-level Codument strict/XML/diff checks passed.

## P4 Coding Attractor Round 2

- Result: PASS with no blocking or non-blocking issue. Cross-slot identity ambiguity is rejected before preflight/import, while legal same-slot reuse remains ready and executable.
- Fresh bounded harness completed in 85.606 seconds with exit 0 and no residual process; all 14 behavior cases, dependency/schema/security/transaction/documentation and Codument consistency checks passed.

## P4 GapLoop

- Round 1 full skeptic review returned `NO_GAP` and wrote `reports/track-impl-gap-report-1.md`.
- Because `verify-round=true`, round 2 ran as a fresh lightweight incremental verification. It also returned `NO_GAP` after bounded build, XML, scoped diff and structural checks, writing `reports/track-impl-gap-report-2.md`.
- GapLoop closed at `gap-round=2` without implementation, behavior or design changes.

## G5-T2 Independent Verification Reopen

- The fresh independent verifier returned `FAIL`: 41 of 44 acceptance criteria passed and all 14 behavior cases passed, but the first externally supervised 120-second complete-harness run timed out at `120.025s` with exit `143` and no residual process.
- A diagnostic 180-second run completed only after `122.866s`. The behavior is therefore not a permanent hang, but it violates the explicit five-run 120-second bound established by `T3.4-AC1` and inherited by final verification.
- The failed watchdog produced no test-level output, so `T3.4-AC2` is also unmet: the current harness does not expose a concrete last test or wait point when the time limit is exceeded.
- `T4.3` reopens only P4 to add opt-in progress/timing diagnostics, isolate and repair the slow path, and re-run five independent 120-second watchdog passes. The limit, existing behavior coverage, and all script-runtime semantics remain unchanged.

## T4.3 Parent Spot Check

- `COZO_OM_TEST_DIAGNOSTICS=1` is opt-in and writes `START`, `WAIT` and `COMPLETE` records to stderr only. A fresh parent-supervised 120-second run built cleanly and completed at 60.115 seconds; its output identified `DEPA red-light detection` (21.880 seconds) as the slowest current top-level matrix and the bounded pending-Promise test (2.003 seconds) as the slowest Jint assertion group.
- The implementation agent's five independent external watchdog runs were all exit 0 at 52.34, 53.52, 48.85, 48.97 and 47.19 seconds, with the normal terminal success line and no lasting matching test process. Parent repeats also completed exit 0 at 55, 73, 75, 59 and 78 seconds. A process observation briefly saw shutdown-race PIDs after some runs, but controlled immediate/one-/three-second follow-up and later scans showed no sustained `Cozo.DotNet.Om.Tests` process.
- Parent build completed with 0 warnings and 0 errors; diff whitespace and package-isolation checks passed. Review confirms diagnostics wrap existing matrix/assertion calls only and default execution remains silent, so no test, behavior case or Jint/OM runtime semantic was removed or weakened.

## P4 Coding Attractor Round 3

- Fresh `coding` profile review returned `PASS` in `reports/p4-attractor-003.md`. It confirmed diagnostics are test-only and default-silent, the Jint wrappers preserve every existing assertion call, and no engine/script dependency crosses into `Om.Core`.
- P4 now enters gap-loop round 3 for a target-backward review of the reopen fix and final verification evidence.
