# P2 Gap Loop Round 3

## Verdict

NO_GAP

## Scope

Fresh post-repair P2 audit for
`sync-dotnet-om-existential-governance-parity`, focused on the round-2 repair:
historical alias storage for entities, edges, and properties; materialize
semantics; deterministic output; and no-init error filtering.

No source, test, track, mission, or prior report files were modified.

## Target-Backward Probe

The current implementation matches the behavior delta and P2 acceptance
criteria.

- Historical entity type storage is covered by `FindViolationsAsync` expanding
  the canonical body and target type sets through `ExpandTypeNamesAsync`. That
  expansion starts from the resolved canonical names plus descendants and adds
  aliases that resolve back to each canonical type.
- Historical edge relation storage is covered by `ExpandRelationNamesAsync`;
  the generated `sat` query accepts every relation name that resolves to the
  canonical relation.
- Historical property storage is covered by `ExpandAttributeNamesAsync`; every
  where predicate accepts the canonical attribute and compatible aliases across
  the applicable body type scopes.
- Materialization uses the same repaired violation search before writing. It
  then writes the Skolem entity, properties, and edge through the resolved
  canonical target type and relation, matching Bun's current-schema write
  semantics while allowing historical data to satisfy the rule.
- Determinism is preserved by ordinal `SortedSet` expansion, sorted alias
  readers, and final violation ordering by rule then entity id.
- No-init behavior remains limited to absent rule storage. `ListExistentialRulesAsync`
  returns an empty list only for missing `om_existential_rule_def` storage
  diagnostics; list/check/apply all flow through that method before rule work.
  Unrelated storage failures are not newly masked.

## Evidence

Reviewed current implementation:

- `cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs`
  - `ApplyExistentialRulesAsync` resolves each materialize rule, uses
    `FindViolationsAsync`, and writes canonical `Exists.ToType` / `Exists.Rel`.
  - `FindViolationsAsync` expands body types, target types, relation names, and
    where attributes before querying.
  - `IsMissingRuleStorage` is scoped to missing rule-storage diagnostics.
- `cozo-lib-dotnet/tests/ExistentialGovernanceParityFixtures.cs`
  - no-init list/check/apply no-op;
  - invalid where operator rejected before persistence;
  - stored-rule alias re-resolution;
  - legacy source entity, target entity, relation edge, and `tier_legacy`
    property created before canonical names and aliases are introduced;
  - canonical post-rename rule reports only `historical:missing`.
- Bun reference:
  - `cozo-lib-bun/cozo-om.js` uses alias-expanded runtime reads and canonical
    materialize writes.
  - `cozo-lib-bun/__tests__/om-existential-versioning.test.js` keeps the
    relation-rename compatibility reference case.

Re-executed current worktree commands:

```text
cd cozo-lib-dotnet
/usr/local/share/dotnet/dotnet build tests/Cozo.DotNet.Om.Tests.csproj --no-restore
# PASS: Build succeeded, 0 warnings, 0 errors.

COZO_OM_TEST_FOCUS=existential-governance \
  /usr/local/share/dotnet/dotnet run --project tests/Cozo.DotNet.Om.Tests.csproj --no-build
# PASS: Focused existential governance fixture passed.

cd ../cozo-lib-bun
/Users/kongweixian/.bun/bin/bun test \
  __tests__/om-existential-define.test.js \
  __tests__/om-existential-versioning.test.js
# PASS: 19 pass, 0 fail, 47 expectations.

cd ..
/Users/kongweixian/.local/bin/codument validate \
  sync-dotnet-om-existential-governance-parity --strict
# PASS: track.xml OK + 1 behavior delta.

git diff --check -- cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs \
  cozo-lib-dotnet/tests/ExistentialGovernanceParityFixtures.cs \
  codument/tracks/sync-dotnet-om-existential-governance-parity
# PASS
```

I did not rerun the full C# OM harness in this round; the existing
`t2.2-correction-verification.md` full-harness evidence remains consistent
with the focused current-worktree verification above.

## Conclusion

No remaining P2 implementation gap was found. Because P2 has
`verify-round="true"` and this is a NO_GAP result after a repair, a fresh
verification round is still required before the parent orchestrator closes the
phase.
