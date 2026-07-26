# P4 Coding Attractor Round 1

## Result

GAP

## Gap 1: Concurrent Registry Writer Lost Update

Import stages from a captured registry snapshot and later publishes unconditionally. Public synchronous registry registration uses only the registry write lock, so a successful registration between capture and publication can be overwritten.

Required repair: perform an atomic reference-identity compare-and-swap under the registry write lock. A mismatch becomes a publication conflict, follows the existing persistent compensation path and preserves the concurrent registry state.

## Gap 2: Cross-Kind Metadata Loss

The common manifest field bag can carry constraint metadata on non-constraints or description/interceptor metadata on constraints. Decode accepts it, while kind-specific persistence ignores it, so round-trip silently drops data.

Required repair: validate the exact metadata field matrix by behavior kind before catalog creation/effects. Invalid JSON and equivalent YAML must return stable diagnostics and leave all state surfaces unchanged.

## Scope

No Jint, G9 facade, YAML dependency or llm-wiki coupling gap was found. T4.1 command and fallback evidence was sufficient.
