# P1 Coding AttractorCheck (Replacement)

## Verdict

PASS

## Independent evidence

Executed only the focused C# existential-governance fixture from the required
working directory:

```text
cd cozo-lib-dotnet
COZO_OM_TEST_FOCUS=existential-governance \
  /usr/local/share/dotnet/dotnet run --project tests/Cozo.DotNet.Om.Tests.csproj --no-build
# Focused existential governance fixture passed.
```

## Static review

- `FindViolationsAsync` resolves each rule at runtime, expands body and target
  type names (including descendants) to canonical names plus all aliases that
  resolve to them, and queries each stored body type deterministically:
  `cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:203-262`.
- Relation aliases and where-attribute aliases are resolved through the same
  compatibility policy and injected as constrained query sets:
  `cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:281-309`.
- The paired fixture stores source and target entities, the edge, and the
  `tier_legacy` property under historical names before defining the canonical
  names and aliases. It proves that the historically satisfied source is not
  a violation while the historical source without an edge is:
  `cozo-lib-dotnet/tests/ExistentialGovernanceParityFixtures.cs:62-92`.
- The uninitialized database path remains explicitly covered for list, check,
  and apply, while missing existential rule storage is treated as empty:
  `ExistentialGovernanceParityFixtures.cs:10-16` and
  `ExistentialRuleLogic.cs:59-78`.

No source, test, track, or mission change was made by this audit.
