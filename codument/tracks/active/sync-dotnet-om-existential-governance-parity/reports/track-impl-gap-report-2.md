# P2 Gap Loop Round 2

## Verdict

GAP

## Finding

The C# runtime re-resolves names embedded in the persisted rule, but it does
not expand a resolved canonical type or relation into all historical stored
aliases when evaluating the rule. `FindViolationsAsync` queries entity rows
only for the canonical body type plus descendants and binds one canonical
relation name. Therefore an entity or edge stored before a rename under a
legacy alias can no longer satisfy the rule and is reported as a violation.

Bun explicitly preserves this compatibility: `_resolveExistentialRuleRuntime`
builds `bodyTypeNames`, `toTypeNames`, and `relNames` from every alias that
resolves to each canonical name, and its versioning test covers a pre-rename
edge satisfying a post-rename rule. The C# fixture only rewrites the stored
rule metadata to aliases, then creates canonical entity/property data; it
does not cover legacy alias values in `om_entity`, `om_edge`, or properties.

## No-init recheck

No gap found in the requested no-init path. `ListExistentialRulesAsync` is the
only storage read used by list/check/apply before rule processing, and its
filter returns empty only for a missing stored relation (including the named
`om_existential_rule_def` diagnostic); unrelated failures remain unhandled.
The focused C# fixture passed its uninitialized list/check/apply case.

## Evidence

```text
cd cozo-lib-dotnet
COZO_OM_TEST_FOCUS=existential-governance dotnet run --project tests/Cozo.DotNet.Om.Tests.csproj --no-build
# Focused existential governance fixture passed.

cd ../cozo-lib-bun
bun test __tests__/om-existential-define.test.js __tests__/om-existential-versioning.test.js
# 19 pass, 0 fail
```

Required repair: have C# runtime evaluation resolve and query every stored
type/relation (and applicable attribute) alias that reaches each canonical
name, with a paired regression equivalent to Bun's pre-rename edge case.
