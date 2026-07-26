# Findings

## Found Facts
- Bun exposes `getEntityViewAsOf(runner, entityId, timestamp)` and assembles an entity view from historical properties plus historical outgoing edges.
- C# exposes `GetPropertyAsOfAsync` and `GetNeighborsAsOfAsync`, but `CozoOm` only exposes `GetEntityViewAsync`; there is no `GetEntityViewAsOfAsync` facade method.
- Bun `findByType(runner, typeName, filter, options)` supports property equality filters and returns entries with `properties`.
- C# `FindByTypeOptions` only has `Exact`; `FindByTypeAsync` returns lightweight `OmEntity` without properties.
- Bun exposes independent validation APIs for property type, relation endpoint compatibility, required properties, constraints, and entity finalization.
- C# has internal property type and relation validation during writes, and `ValidateEntityAsync`, but does not expose facade APIs for `ValidatePropertyTypeAsync`, `ValidateRelationAsync`, `ValidateRequiredPropertiesAsync`, or `FinalizeEntityAsync`.
- Bun computed properties are resolved through the ancestor chain and merged into entity views with child definitions overriding parent definitions.
- C# computed properties in entity views currently list only `om_computed_def` rows for the exact entity type and resolve runtime delegates only by exact type.
- Bun constraints support `conditional`, `cross-entity`, and `computed-dep` runtime callbacks with inherited constraints.
- C# `ValidateEntityAsync` currently executes only constraints whose stored `constraint_type` is `custom`, and only with exact-type registry lookup.

## Constraints
- The change must preserve existing .NET OM behavior and add parity without breaking public callers.
- Public APIs should remain idiomatic C# and avoid exposing Bun-specific JavaScript callback shapes directly.
- Runtime callback delegates remain in-memory; persisted metadata is not enough to restore delegate behavior after process restart.
- Relation/property writes must keep the existing transactional and rollback behavior when validation fails.
- Tests must cover both .NET behavior and the Bun reference behavior where parity is being asserted.

## Open Questions
- Whether `findByType` should change its existing return type or add a second API that returns entries with properties. To avoid breaking existing callers, the conservative assumption is to add a new typed result/API or overload while preserving the old method.
- Whether C# should retain `custom` as an accepted alias for conditional validators. The conservative assumption is yes, for backward compatibility.

## Conclusions
- The missing surface is real and scoped to OM core parity plus adjacent behavior-layer parity.
- The track should implement C# additions for historical entity views, property-filtered type search, public validation helpers, computed inheritance, and typed constraint scopes.
- The track should add or extend tests on both sides so Bun continues to document the reference behavior and C# proves parity.

## Implementation Notes
- Added C# public APIs: `GetEntityViewAsOfAsync`, `FindByTypeWithPropertiesAsync`, `ValidatePropertyTypeAsync`, `ValidateRelationAsync`, `ValidateRequiredPropertiesAsync`, `FinalizeEntityAsync`, and typed `DefineConstraintAsync` / `RegisterConstraint` support.
- Added `FindByTypeEntry` as the rich type-search result so the existing lightweight `FindByTypeAsync` contract remains intact.
- Added as-of entity view assembly from historical properties and outgoing edges; computed properties use `OmComputedContext.AsOf` so context `GetPropertyAsync` reads historical values.
- Changed computed lookup to merge ancestors before the concrete type, allowing inherited computed properties and child overrides.
- Changed property writes to wrap write-plus-validation in a transaction when constraints are not skipped, so failed validation rolls back the written property.
- Changed relation writes to wrap write-plus-cross-entity-validation in a transaction when constraints are not skipped, so failed cross-entity validation rolls back the written edge without adding required-property validation to link operations.
- Added effective constraint collection from ancestors plus concrete type, with child constraints overriding parent constraints by name.
- Added Bun tests for `findByType` property filters/properties and validation helper exports/behavior.

## Verification Notes
- `xmllint --noout` passed for this track's `track.xml` and behavior delta.
- `git diff --check` passed for the modified C#, Bun test, and track files.
- Bundled Node `--check` passed for added/modified Bun test files.
- `.NET` tests were not executed because `dotnet`, `csc`, and `mcs` are not available in PATH.
- Bun tests were not executed because `bun` is not available in PATH.
- `codument validate add-dotnet-om-core-parity-apis --strict` was not executed because `codument` is not available in PATH.
