# Findings

## Found Facts

- Bun type-system reference semantics are recorded in `codument/archive/2026-02-28-add-type-hierarchy/spec.md` and covered by Bun tests under `cozo-lib-bun/__tests__/`.
- .NET `TypeLogic.GetAttributeDefinitionsAsync` currently merges ancestors and self but not mixin-contributed attributes.
- .NET `TypeLogic.DefineAttributeAsync` currently does not reject inherited attribute value-type changes or required loosening.
- .NET `EntityLogic.ValidatePropertyTypeAsync` currently has no `Validity` special case, so `OmValueType.Validity` values cannot be set through ordinary property writes.
- .NET property reads currently resolve aliases to canonical names but do not fall back to legacy alias-stored rows.
- .NET public facade does not expose Bun-like type-system inspection APIs such as resolve helpers or effective attribute definitions.

## Constraints

- Preserve `Om.Core` DEPA boundaries: logic uses `ICozoOmStore`; direct Cozo effects stay in support.
- Cozo stored relations remain the authoritative schema and instance facts.
- Existing dirty worktree changes outside this scope must not be reverted.

## Open Questions

- Whether relation `directed=false` should align to Bun's current forward-only validation or .NET's current reverse endpoint acceptance remains a mission-level reconciliation item.

## Conclusions

- P0 implementation should first fix mixin attributes, inherited attribute restrictions, alias fallback, `Validity` attributes, and parent preservation on type redefinition.
