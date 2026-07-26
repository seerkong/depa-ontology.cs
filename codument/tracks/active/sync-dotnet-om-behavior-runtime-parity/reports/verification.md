# Verification Report

Verified on 2026-07-16 from `/Users/kongweixian/infra-dev/cozodb/cozo`.

| Check | Working directory | Command | Exit | Result |
|---|---|---|---:|---|
| .NET runtime | repository root | `/usr/local/share/dotnet/dotnet --version` | 0 | `10.0.103` |
| C# build | `cozo-lib-dotnet` | `MSBUILDTERMINALLOGGER=off /usr/local/share/dotnet/dotnet build tests/Cozo.DotNet.Om.Tests.csproj --no-restore` | 0 | 0 warnings, 0 errors |
| C# OM harness | `cozo-lib-dotnet` | `MSBUILDTERMINALLOGGER=off /usr/local/share/dotnet/dotnet run --project tests/Cozo.DotNet.Om.Tests.csproj --no-build` | 0 | `Cozo.DotNet OM tests passed.` |
| Bun runtime | repository root | `/Users/kongweixian/.bun/bin/bun --version` | 0 | `1.3.6` |
| Bun reference suites | `cozo-lib-bun` | `/Users/kongweixian/.bun/bin/bun test __tests__/om-action.test.js __tests__/om-interceptor.test.js __tests__/om-mutation.test.js` | 0 | 19 pass, 0 fail, 38 assertions, 3 files |
| Diff whitespace | repository root | `git diff --check` | 0 | no output |
| XML parse | repository root | `xmllint --noout codument/tracks/sync-dotnet-om-behavior-runtime-parity/track.xml codument/tracks/sync-dotnet-om-behavior-runtime-parity/behavior_deltas/cozo-dotnet-om/delta.xml codument/missions/active/converge-dotnet-om-bun-capability-parity/mission.xml` | 0 | all documents parsed |

The external `codument` CLI was not found on `PATH`; its best-effort strict validation step was skipped according to the workspace fallback policy. XML structure was checked with `xmllint` and will receive an independent final gap review.
