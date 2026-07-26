# P3 Gap Loop Round 1

## Finding

`InitializeSchemaV2Input` and `RollbackSchemaV2Input` initially cached their
`EffectiveOptions` value in an initialized property. Because these are
positional records, a caller using `with { Options = ... }` would receive a
copy retaining the old effective value.

## Repair

Changed both values to computed properties and added a regression assertion
for `InitializeSchemaV2Input` constructed through a `with` expression.

## Result

The schema-evolution focused harness passes after the repair. This is a real
contract gap fixed during P3, so another verification round is required by
the track's `verify-round=true` hook.
