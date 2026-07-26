# Findings

## Found Facts

- Bun `callParentAction` starts above the current action owner and returns parent mutations without executing them.
- C# action and mutation lookup already resolve concrete type before ancestors.
- C# interceptor collection globally sorts owner-local sequence values after flattening, which can interleave parent and child registrations.
- C# behavior metadata writes do not consistently validate owner type or constraint scope first.
- The callback interceptor overload registers the callback before persistent validation, allowing ghost registry state on failure.

## Constraints

- Preserve the C# transactional execution boundary and typed registry.
- Do not absorb G4 portability or G9 public facade work.
- Add tests on both C# and Bun surfaces.
- `dotnet` is available at `/usr/local/share/dotnet/dotnet`; `bun` discovery remains pending.

## Open Questions

- None. Mission decisions D0/D4 cover the implementation direction.

## Conclusions

- G3 requires code changes for parent dispatch, interceptor ordering, and definition guardrails, not only tests.
- Public validation facade work remains outside this track.

## T1.1 Spot Check

- Added definition guardrails for missing owner types, unknown constraint scopes, unknown interceptor phases, and null callback overloads.
- Callback-coordinating helpers compensate newly persisted metadata when registry registration fails and avoid registering callbacks when persistent writes fail.
- Focused tests cover all five behavior definition kinds, invalid scope/phase, null callbacks, persistent interceptor failure, and simulated registry failure.
- Parent verification: `dotnet build tests/Cozo.DotNet.Om.Tests.csproj --no-restore` completed with 0 warnings and 0 errors; `dotnet run --project tests/Cozo.DotNet.Om.Tests.csproj --no-build` printed `Cozo.DotNet OM tests passed.`; `git diff --check` passed.
- The contract does not currently promise restoration of prior metadata contents when replacing an existing definition fails after persistence. The implementation restores an existing callback registration, but this broader overwrite transaction remains an explicit residual risk rather than a tested guarantee.

## T1.2 Spot Check

- `OmActionContext.CallParentActionAsync` resolves from the parent of the current `ActionOwnerType`, invokes only the nearest parent handler, and advances the owner in the nested context.
- Tests cover direct and three-level parent calls, missing-parent diagnostics, deferred parent mutations, no duplicate interceptor execution, and atomic rollback of composed parent/child effects.
- Parent verification: `dotnet build tests/Cozo.DotNet.Om.Tests.csproj --no-restore` completed with 0 warnings and 0 errors; the complete OM executable test suite passed; `git diff --check` passed.
- Bun baseline runtime is available at `/Users/kongweixian/.bun/bin/bun`; the existing action/interceptor/mutation suites pass with 13 tests and 0 failures.

## T1.3 Spot Check

- Removed the final global `OrderBy(Seq)` from flattened inherited interceptors; root-first owner grouping now remains intact while registry-local ordering handles each owner's sequence.
- Tests use two parent and two child interceptors in each phase so owner-local sequences overlap and the previous interleaving bug would fail deterministically.
- Inherited before failure prevents action/mutation execution and commits no writes; inherited after failure runs after mutation work but rolls the whole action transaction back.
- Parent verification: `dotnet build tests/Cozo.DotNet.Om.Tests.csproj --no-restore` completed with 0 warnings and 0 errors; the complete OM executable test suite passed; `git diff --check` passed.

## P1 AttractorCheck 1

- Verdict: GAP.
- A callback registration failure while replacing an existing constraint, action, mutation, or interceptor restores the old callback but leaves the newly written metadata values in place.
- This violates the phase's bidirectional atomicity direction and can make registry behavior disagree with persisted scope/message/description. T1.1 was reopened for snapshot restoration and focused overwrite-failure tests.

## P1 GAP Repair

- All four callback coordination paths now capture the previous persisted payload and callback before overwrite, then restore both on registration failure.
- Metadata restoration uses raw relation writes so historical payloads, including a legacy unsupported constraint scope, can be restored without passing through tightened definition validation.
- If registry or metadata compensation itself fails, an `AggregateException` preserves both the original registration error and rollback errors instead of claiming atomic restoration.
- Focused tests assert exact old metadata values and callback identities for constraint, action, mutation, and interceptor overwrite failures. Parent build and the complete OM executable test suite pass; `git diff --check` passes.

## P1 AttractorCheck 2

- Verdict: PASS.
- Fresh review found no remaining issue that blocks completion of the behavior-runtime implementation phase.

## T2.1 Spot Check

- Audited every C# behavior-delta case against concrete assertions in the executable OM test harness.
- Added explicit nearest inherited action and nearest inherited mutation tests; the mutation scenario registers the same name on root and middle and proves the child action resolves only the middle implementation.
- Parent verification: build completed with 0 warnings and 0 errors, the complete OM executable test suite passed, and `git diff --check` passed.

## T2.2 Spot Check

- Bun tests now cover three-level parent dispatch and owner advancement, composed parent/child mutation commit and rollback, and nearest inherited mutation resolution.
- Bun interceptor tests now use overlapping parent/child local sequences in both phases and assert exact ancestor-to-child grouping; inherited before and after failures assert transactional effects.
- Parent verification: `/Users/kongweixian/.bun/bin/bun test __tests__/om-action.test.js __tests__/om-interceptor.test.js __tests__/om-mutation.test.js` completed with 19 passing tests, 0 failures, and 38 assertions; `git diff --check` passed.

## P2 AttractorCheck

- Verdict: PASS.
- Fresh review found no P0/P1 test gap that blocks completion of the paired behavior-runtime test phase.

## T3.1 Independent Verification

- .NET SDK `10.0.103`: build exited 0 with 0 warnings and 0 errors; the complete OM executable test harness exited 0 and printed `Cozo.DotNet OM tests passed.`
- Bun `1.3.6`: the three target suites exited 0 with 19 passing tests, 0 failures, and 38 assertions.
- `git diff --check` exited 0 with no output.

## T3.2 Boundary Review 1

- Verdict: GAP on evidence presentation only; no product-surface, scope, sequencing, ordering, XML, or diff-boundary defect was found.
- The existing T3.1 findings contained versions and exit summaries, but did not list exact working directories and commands. `reports/verification.md` now records each command, cwd, exit code, and result, plus the external Codument CLI fallback.

## T3.2 Boundary Review 2

- Verdict: GAP in mission owner documentation only.
- `mission.xml` and the active track assign public validation facade work to G9, but the mission design retained an older sentence assigning it to G3. Mission revision 6 and `reports/replan-002.md` now align the design with the approved G3/G9 split; no product code crossed that boundary.

## T3.2 Boundary Review 3

- Verdict: PASS.
- The mission owner documents, verification evidence, public surface, file scope, and G3/G9 boundary are now consistent and independently reviewable.

## P3 AttractorCheck

- Verdict: PASS.
- Fresh coding-profile review found no P0/P1 issue blocking P3 or track completion.

## P3 GapLoop

- Round 1: `FIX_APPLIED`. The public `CozoOmRegistry.RegisterInterceptor` overload still mapped unsupported phases to `before`; strict target selection and a no-residual regression test repaired the gap.
- Round 2: `NO_GAP`. Lightweight recheck confirmed invalid phases fail before collection mutation, legal phases still work, and the complete C# OM harness passes.
- Final parent verification repeated `git diff --check`, XML parsing, C# build, and the complete C# OM harness with exit code 0.
