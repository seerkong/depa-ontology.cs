# Implementation Report

## Result

The runtime registry lifecycle contract is implemented and verified in Bun and .NET.

## Bun

- Added `createOmRuntime(runner)` as the callback registry lifecycle owner.
- Replaced module-global behavior registries with per-runtime immutable snapshots.
- Preserved plain-runner calls through one explicit legacy registry adapter.
- Added targeted `clearRegistry(runtime)` while keeping no-argument legacy clear.
- Propagated one resolution snapshot through transactions, action inheritance, parent action calls, mutations, interceptors, constraints, computed properties, and callback `ctx.runtime`.
- Preserved raw `ctx.runner` for compatibility.

## .NET

- Added `CozoOm.ClearRegistryAsync(CancellationToken)`.
- Clear enters the behavior gate and atomically publishes `CozoOmRegistrySnapshot.Empty`.
- Persistent behavior definitions and callback binding identities remain unchanged.
- Catalog readiness is derived after clear and changes from `Ready` to `Unresolved`.

## Verification

- `bun test __tests__/om-runtime-registry-lifecycle.test.js`: 4 passed, 27 expectations.
- `bun test __tests__/om-*.test.js`: 214 passed, 633 expectations.
- `bun test`: 279 passed, 735 expectations.
- `dotnet run --project tests/Cozo.DotNet.Om.Tests.csproj --no-restore`: passed with `Cozo.DotNet OM tests passed.`
- `node -c cozo-lib-bun/cozo-om.js`: passed.
- `git diff --check` for the implementation files: passed.
- `codument validate unify-om-runtime-registry-lifecycle --strict`: passed.

## Compatibility

- Bun legacy callers continue to share only the legacy adapter registry.
- Explicit runtimes do not share callback state even when they share one database.
- In-flight behavior commands finish on their captured snapshot; later commands observe clear.
- No callback body or script source is persisted.
