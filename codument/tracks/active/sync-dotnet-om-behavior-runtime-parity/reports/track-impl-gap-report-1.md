# Track Implementation Gap Report 1

## Scope

- Track: `sync-dotnet-om-behavior-runtime-parity`
- Phase: `P3`
- Round: `1`
- Mission boundary: revision 6; G3 owns behavior runtime and definition guardrails, while G4-G9 remain excluded.

## Verdict

`FIX_APPLIED`

## Gap

The callback-based `CozoOm.AddInterceptorAsync` path validated phases before persistence, but the existing public typed-registry method `CozoOmRegistry.RegisterInterceptor` still treated every non-`after` phase as `before`. A caller could therefore register an executable before interceptor through the public registry with an unsupported phase, contradicting the behavior delta's no-fallback guardrail and the track's public-surface claim.

## Repair

- Reopened T1.1 while repairing the guardrail, then restored it to `DONE` after verification.
- Routed public interceptor registration through strict phase selection and validated the callback before registry collection mutation.
- Added a C# regression assertion proving an unsupported public-registry phase throws and leaves no before registration.
- Did not change parent dispatch, transaction ownership, nearest mutation lookup, interceptor owner grouping, Bun runtime code, or any G4-G9 surface.

## Target Comparison

- Definition owner/scope/phase/callback guardrails and first-time/overwrite compensation are covered by the C# harness.
- Parent action owner advances registration-by-registration and stays inside the outer action transaction.
- Mutation resolution remains nearest-owner from the concrete entity type.
- Before and after interceptors remain ancestor-to-child with owner-local sequence order.
- Bun reference tests cover parent composition, nearest inherited mutation, inherited ordering, and rollback.
- Mission revision 6, track artifacts, completion status, and verification evidence now agree on the G3/G9 boundary.

## Verification

- `MSBUILDTERMINALLOGGER=off /usr/local/share/dotnet/dotnet build tests/Cozo.DotNet.Om.Tests.csproj --no-restore`: exit 0, 0 warnings, 0 errors.
- `/usr/local/share/dotnet/dotnet tests/bin/Debug/net10.0/Cozo.DotNet.Om.Tests.dll`: exit 0, `Cozo.DotNet OM tests passed.`
- `/Users/kongweixian/.bun/bin/bun test __tests__/om-action.test.js __tests__/om-interceptor.test.js __tests__/om-mutation.test.js`: exit 0, 19 pass, 0 fail, 38 assertions.
