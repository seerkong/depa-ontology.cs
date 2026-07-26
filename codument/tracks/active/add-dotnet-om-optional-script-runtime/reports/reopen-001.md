# Track Reopen Report

## Trigger

Independent `G5-T2` verification recorded a blocking failure in `reports/verify-report.md`: a fresh full OM harness run timed out at `120.025s` under the required external watchdog, while an extended diagnostic run passed only after `122.866s`.

## Desired State

The full harness must complete five consecutive externally supervised runs within 120 seconds, leave no process behind, and provide enough progress output to identify a concrete test or wait point if the bound is exceeded.

## Actual State

All script-runtime behavior cases passed, but the current harness prints only at completion. The failed watchdog therefore had an empty output tail and could not identify the slow location.

## Applied Change

The track is reopened as `in_progress`. P4 is `ACTIVE`, and `T4.3` is the sole active task. It adds bounded diagnostic observability and requires evidence-backed remediation only; it must not relax the 120-second threshold, skip tests, or weaken behavior coverage.

## Next Observation

Run `T4.3`, independently spot-check its five watchdog results, then repeat the `G5-T2` verification.
