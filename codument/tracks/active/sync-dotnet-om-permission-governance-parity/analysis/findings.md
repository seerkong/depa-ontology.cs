# Findings

## Found Facts

- Bun `checkAccess` resolves each declared relation path against subject-to-resource graph edges, records a deterministic witness, and rejects policies without one.
- Bun ABAC resolves `subject.*` and `resource.*` at an optional temporal point, supports equality and ordered comparisons, treats unsupported operators as policy failure, emits field visibility, and records per-policy explanation detail.
- C# `CheckAccessAsync` currently loads matching policies and includes configured path text in the explanation without resolving graph edges. Its ABAC helper only handles simple references and `=`, `==`, `!=`; `CheckAccessInput.AsOf` is not evaluated.
- Permission metadata is persisted in `om_perm_policy`, `om_perm_path_rule`, and `om_perm_abac_rule`; schema snapshots already cover these relations.

## Constraints

- D1=A is resolved: strict Bun-compatible path witness and full ABAC are a hard cut, fail closed, with no permissive legacy evaluation mode.
- D0 permits idiomatic typed async C# APIs, but observable authorization outcomes, temporal interpretation, diagnostics and field visibility must align.
- Preserve deny-overrides-allow and existing transactional/schema snapshot mechanisms.

## Conclusions

- Implement strict evaluation in `Om.Core` rather than adding an adapter: policy metadata and `CheckAccessAsync` are C# core contracts.
- Characterization tests must first demonstrate the current path-text false allow, then replace it with witness-required behavior and two-sided Bun/C# fixtures.

## T1.1 Characterization

- Bun's focused permission suite and new conformance suite pass 11/11 tests.
- C# builds with 0 warnings/errors, then correctly red-fails the first strict fixture: no `permission_owns` edge still returns `Allow=True` and explanation only echoes `witnessPaths`. The fixture has no skip or conditional assertion, so it is the controlled P2 implementation target rather than a passing claim.
- The new fixtures also lock temporal graph/property, ordered comparator/deny, field hide, invalid-policy and explanation-order behavior for later phases.

## P3 Closure

- Final evidence: Bun full suite passed 270/270; C# built with zero warnings and
  errors, the full OM harness passed in 189.724 seconds, and the focused
  permission-governance matrix passed in 2.768 seconds.
- T3.1's harness-only parent-composition timeout was a `Task.WhenAny` guard
  scheduling race. T3.1.1 uses `Task.WaitAsync` with the unchanged three-second
  deadline and regression cases for completion and stalled work; runtime limits
  and behavior assertions are untouched.
- P3 AttractorCheck and two configured gap-loop rounds found no remaining
  permissive authorization or temporal consistency gap.
