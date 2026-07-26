# P2 Coding AttractorCheck

## Verdict

PASS

## Scope Audited

Only the track's three claimed governance behaviors were reviewed:

1. Unsupported existential `where` operators are rejected before rule persistence.
2. Persisted historical rule metadata is resolved through the current type, relation, target-type, and attribute aliases at evaluation time.
3. `list`, `check`, and `apply` are safe before existential rule storage has been initialized.

## Code Evidence

- `DefineExistentialRuleAsync` normalizes the complete spec before either the materialize-only attribute write or the `om_existential_rule_def` `:put` ([ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:21), [ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:39), [ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:47)). `NormalizeSpecAsync` has an explicit allow-list for `=`, `!=`, `>`, `>=`, `<`, and `<=`, and throws for every other operator ([ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:179)).
- `FindViolationsAsync` resolves the loaded rule on every evaluation ([ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:206)); `ResolveRuntimeRuleAsync` re-resolves body type, relation, target type, and each `where` attribute ([ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:261)). Chase materialization also uses the resolved rule for link and result metadata ([ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:119)).
- `ListExistentialRulesAsync` maps only missing `om_existential_rule_def` storage failures to `[]` ([ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:60), [ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:285)). Both `CheckExistentialRulesAsync` and `ApplyExistentialRulesAsync` enumerate that list before executing any per-rule operation, so their no-init paths are empty-result no-ops ([ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:87), [ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:105)).
- The focused fixture exercises all three boundaries: fresh no-init list/check/apply, rejected `contains` with postcondition of no persisted rule, and a manually persisted rule containing legacy type/relation/attribute aliases ([ExistentialGovernanceParityFixtures.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/tests/ExistentialGovernanceParityFixtures.cs:11), [ExistentialGovernanceParityFixtures.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/tests/ExistentialGovernanceParityFixtures.cs:27), [ExistentialGovernanceParityFixtures.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/tests/ExistentialGovernanceParityFixtures.cs:37)).

## Independent Execution

```text
cd cozo-lib-dotnet
COZO_OM_TEST_FOCUS=existential-governance \
  /usr/local/share/dotnet/dotnet run --project tests/Cozo.DotNet.Om.Tests.csproj --no-build
# Focused existential governance fixture passed.

cd ../cozo-lib-bun
/Users/kongweixian/.bun/bin/bun test \
  __tests__/om-existential-define.test.js \
  __tests__/om-existential-check.test.js \
  __tests__/om-existential-chase.test.js \
  __tests__/om-existential-fixpoint.test.js \
  __tests__/om-existential-temporal.test.js \
  __tests__/om-existential-versioning.test.js
# 43 pass, 0 fail, 102 assertions
```

The C# behavior is aligned with the Bun reference's operator allow-list and safe no-init coverage. No GAP or BLOCKED condition was found within the requested scope.
