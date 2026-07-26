# P2 Replacement Coding AttractorCheck

## Verdict

PASS

## Scope

Independent post-correction audit of the G8 existential-governance track.
No source, test, track, or mission state was modified.

## Findings

- **Fail-closed operators:** definition normalization accepts only `=`, `!=`,
  `>`, `>=`, `<`, and `<=`; any other operator throws before persistence.
  Runtime query generation repeats the closed switch and throws for a manually
  persisted unsupported operator, so an unsupported predicate cannot silently
  match or materialize data.
- **Historical alias storage:** runtime resolution canonicalizes persisted rule
  names first, then expands the canonical body and target types (including
  descendants), relation, and where attributes to every alias that resolves to
  each canonical name. `SortedSet` plus ordinal-sorted alias readers makes the
  expanded query inputs deterministic. The focused fixture stores legacy
  source/target entities, a legacy edge, and `tier_legacy` before aliases are
  introduced; a canonical post-rename rule recognizes the satisfied legacy
  source and reports only `historical:missing`.
- **Materialization alignment:** `ApplyExistentialRulesAsync` resolves each
  rule before choosing the canonical target/relation for writes, while its
  violation search uses the same alias expansion as checking. Thus historical
  data can satisfy a materialize rule, and new Skolem output is written through
  the current canonical schema.
- **No-init safety:** list/check/apply all reach `ListExistentialRulesAsync`
  before rule evaluation; a missing `om_existential_rule_def` relation is
  treated as an empty list, while unrelated storage failures are not masked.

## Independent Evidence

```text
cd cozo-lib-dotnet
/usr/local/share/dotnet/dotnet build tests/Cozo.DotNet.Om.Tests.csproj --no-restore
# PASS: Build succeeded, 0 warnings, 0 errors.

COZO_OM_TEST_FOCUS=existential-governance \
  /usr/local/share/dotnet/dotnet run --project tests/Cozo.DotNet.Om.Tests.csproj --no-build
# PASS: Focused existential governance fixture passed.

cd ..
/Users/kongweixian/.local/bin/codument validate \
  sync-dotnet-om-existential-governance-parity --strict
xmllint --noout codument/tracks/sync-dotnet-om-existential-governance-parity/track.xml
git diff --check -- cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs \
  cozo-lib-dotnet/tests/ExistentialGovernanceParityFixtures.cs \
  codument/tracks/sync-dotnet-om-existential-governance-parity
# PASS
```

The prior full-harness, Bun reference, and correction evidence in
`t2.2-correction-verification.md` remains consistent with this independent
focused audit. No remaining gap was found in the requested scope.
