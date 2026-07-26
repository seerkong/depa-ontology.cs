# Type-System Parity Verification

## Gap Matrix

| Gap | Reference | .NET Result |
|---|---|---|
| Mixin attributes are effective attributes | Bun `om-mixin.test.js`, archived type hierarchy spec | Implemented in `TypeLogic.GetAttributeDefinitionsAsync`; test covers write/read through public API |
| Inherited attribute override safety | Bun `om-attr-inheritance.test.js` | Implemented in `TypeLogic.DefineAttributeAsync`; test covers required loosening and value-type change rejection |
| Type redefinition preserves parent when parent omitted | Bun `defineType` existing-type behavior | Implemented in `TypeLogic.DefineTypeAsync`; test covers parent preservation |
| Alias-stored property fallback | Bun `om-alias-resolution.test.js` | Implemented in `EntityLogic.GetPropertyAtAsync` and validation canonicalization |
| Validity attribute values | Bun `om-validity-attr.test.js` | Implemented in `OmConvert` and `EntityLogic.SetPropertyAsync`; test verifies `to_int(validity)` micros |
| Polymorphic aggregation | Bun `aggregateByType` behavior | Implemented as `CozoOm.AggregateByTypeAsync`; test covers default descendants and exact mode |
| Undirected relation endpoint matching | `om_rel_def.directed=false` shared schema field | Bun `validateRelation` now accepts reverse endpoint typing; .NET already did and now has explicit coverage |
| Entity type existence on create/upsert | .NET rejects unknown type names | Bun now rejects unknown canonical type names after alias resolution; both sides have tests |
| Hierarchy and alias facade coverage | Bun hierarchy/alias tests | .NET tests now cover hierarchy APIs, type/relation/attribute alias resolution, alias cycles, relation-alias linking, and alias-stored entity canonical views |
| Description and mixin preservation coverage | Bun description/mixin tests | .NET tests now cover attribute description precedence and mixin preservation when redefining a type without options |
| Aggregate operator matrix | Bun/.NET aggregate APIs | Both sides now cover `sum`, `avg`, `min`, `max`, `count`, default descendants, and exact mode |

## Verification Commands

- `dotnet build cozo-lib-dotnet/Cozo.DotNet.csproj -tl:off`
- `dotnet build cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj -tl:off`
- `dotnet run --project cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj`
- `bun test __tests__/om-rel-inheritance.test.js __tests__/om-attr-inheritance.test.js __tests__/om-mixin.test.js __tests__/om-alias-resolution.test.js __tests__/om-validity-attr.test.js __tests__/om-polymorphic-query.test.js` in `cozo-lib-bun/`
- `bun test __tests__/om-*.test.js` in `cozo-lib-bun/`
- `codument validate audit-dotnet-om-type-system-parity --strict`

## Results

All commands passed.

## Residual Risk

No known P0 type-system parity gap remains from the mission's initial audit list. Broader API shape parity, such as returning property bags from `FindByTypeAsync`, can be handled as an ergonomic follow-up if product usage requires exact Bun facade shape.
