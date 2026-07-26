# P1 Coding Attractor Round 1

## Result

GAP

## Evidence

1. `BehaviorCatalogLogic.ProjectBinding` joined persisted binding rows to mutable registry entries only by behavior key, so a different callback identity at the same key could project `Ready`.
2. Public catalog and decode-result records accepted `IReadOnlyList` values without defensive copying, which did not satisfy the design's immutable API requirement.

## Applied Revision

- Added T1.3 to make registry callback entries carry binding identity and to require an exact identity match for readiness.
- Added T1.3 coverage for truly immutable catalog, callback and diagnostic collections.
- Kept execution gates, staged import, YAML and script runtime outside P1.

## Tooling

The independent reviewer reported build, OM harness and `git diff --check` green. The external `codument validate --strict` command is unavailable in this checkout, so structural validation remains best-effort XML parsing plus later lifecycle verification.
