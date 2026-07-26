# P2 Gap Loop Round 1

## Verdict

NO_GAP

## Target-backward audit

The implementation satisfies the behavior delta and every P2 acceptance
criterion without a repair:

- Definition-time normalization allow-lists `=`, `!=`, `>`, `>=`, `<`, and
  `<=` before any rule-storage write. Unsupported operators fail with a
  `CozoException`; the focused fixture confirms that `contains` leaves no
  persisted rule.
- Evaluation calls `ResolveRuntimeRuleAsync` from `FindViolationsAsync` for
  each loaded rule. It resolves body type, relation, target type, and every
  `where` attribute against the current alias registry. Chase execution also
  resolves the loaded rule before materializing, so historical alias metadata
  cannot bypass current canonical names.
- `ListExistentialRulesAsync` converts only the missing
  `om_existential_rule_def` storage failure into an empty set. `check` and
  `apply` both derive their work from that list, so they return safe empty
  results before schema initialization while unrelated storage errors remain
  visible.

## Re-executed evidence

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

Because this is the first `NO_GAP` round and P2 configures
`verify-round="true"`, a fresh lightweight verification round is still
required before the phase can close.
