# P1 Coding AttractorCheck

## Status

BLOCKED

Static audit: PASS. The requested runtime command did not complete or emit a
result during the review window, so runtime execution is not independently
confirmed by this check.

## Scope and evidence

- Track intent and acceptance: `track.xml`, `proposal.md`, `design.md`, and
  `reports/t1.3-historical-alias-storage.md` require runtime re-resolution of
  body type, target type, relation, and applicable where attributes, including
  historical storage; deterministic output; materialization consistency; cycle
  tolerance; and unchanged no-init behavior.
- Body and target type expansion resolves each current canonical type and
  descendants, adds every historical alias that resolves to that canonical,
  deduplicates with ordinal `SortedSet`, and queries each exact stored name
  ([ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:207), [ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:265)).
- Relation expansion follows the same current-resolution rule and supplies
  canonical plus compatible historical relation names to the edge query
  ([ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:281)).
- Each where predicate expands attributes across every applicable body-type
  scope, including aliases resolving to the canonical attribute; the query
  constrains the selected attribute name to that set
  ([ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:223), [ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:292)).
- Final violations are sorted by rule then entity, and each query is sorted by
  entity id ([ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:87), [ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:246)).
- Materialization re-resolves the rule, writes the canonical target type,
  canonical relation, and uses the same violation path before its post-check
  ([ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:117), [ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:141), [ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:153)).
- Alias enumeration catches resolution errors for cyclic aliases and skips
  those aliases, matching the Bun reference’s cycle-skipping alias-list
  helpers ([ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:339), [cozo-om.js](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-bun/cozo-om.js:3197), [cozo-om.js](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-bun/cozo-om.js:3264)). Direct resolution of a rule name that itself is cyclic remains an error in both implementations.
- Missing existential rule storage is converted to an empty list, and the
  fixture checks list/check/apply on an uninitialized database
  ([ExistentialRuleLogic.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/src/Om.Core/Logic/ExistentialRuleLogic.cs:60), [ExistentialGovernanceParityFixtures.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/tests/ExistentialGovernanceParityFixtures.cs:10)).
- The fixture also covers invalid-operator non-persistence and a legacy entity,
  target, edge, and property that are created before canonical names and aliases;
  its expected result is only the legacy source lacking the edge
  ([ExistentialGovernanceParityFixtures.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/tests/ExistentialGovernanceParityFixtures.cs:27), [ExistentialGovernanceParityFixtures.cs](/Users/kongweixian/infra-dev/cozodb/cozo/cozo-lib-dotnet/tests/ExistentialGovernanceParityFixtures.cs:62)).

## Execution result

Attempted from `cozo-lib-dotnet`:

```text
/usr/local/share/dotnet/dotnet build tests/Cozo.DotNet.Om.Tests.csproj --no-restore
COZO_OM_TEST_FOCUS=existential-governance /usr/local/share/dotnet/dotnet run --project tests/Cozo.DotNet.Om.Tests.csproj --no-build
```

The process remained silent beyond the review wait window and was cancelled;
no pass/fail output was obtained. This is an execution/environment blocker,
not evidence of a source or fixture failure.

## Gaps

No source, test, track, mission, or Bun-file gap was established by the static
audit. No unrelated changes are suggested or applied.
