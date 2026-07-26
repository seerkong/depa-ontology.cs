# Gap Loop Report 1

## Scope

- Track: `add-dotnet-om-depa`
- Execution mode: main agent context, per user instruction. This intentionally deviates from the default fresh-subagent protocol because the user explicitly asked to execute the track and gap-loop in the main context.

## Verdict

`FIX_APPLIED`

## Gaps Found

1. Attribute alias support was incomplete: aliases could be resolved only for the exact entity type, so child types did not inherit parent attribute aliases.
2. Schema versioning had snapshot/diff support but no rollback operation.
3. Permission/access check was a placeholder and did not provide observable allow/deny, field scope, ABAC, or witness path behavior.

## Fixes Applied

1. Added facade methods for type, relation, and attribute alias definition and fixed inherited attribute alias resolution.
2. Added schema rollback from stored snapshots across core metadata relations.
3. Added permission policy, ABAC rule, and path rule APIs plus an evaluator with deny-overrides-allow semantics, inherited resource type scopes, field resource scopes, and witness path output.
4. Expanded the .NET OM console test harness to cover aliases, rollback, permission allow/deny/field/path behavior, temporal reads/retracts, constraints, and existential rule materialization.

## Evidence

- `MSBUILDTERMINALLOGGER=off dotnet build cozo-lib-dotnet/Cozo.DotNet.csproj`
- `MSBUILDTERMINALLOGGER=off dotnet run --project cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj`
- `codument validate add-dotnet-om-depa --strict`
- `rg -n "CozoDb|CozoNative" cozo-lib-dotnet/src/Om/Logic cozo-lib-dotnet/src/Om/Runtime cozo-lib-dotnet/src/Om/Inputs cozo-lib-dotnet/src/Om/Contracts cozo-lib-dotnet/src/Om/Internals || true`
