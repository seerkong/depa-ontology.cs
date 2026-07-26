# Knowledge Context

## Source Notes

| Source | Summary | Relevance |
|--------|---------|-----------|
| `cozo-lib-bun/cozo-om.js` | Reference implementation for type hierarchy, mixins, aliases, and validity attributes. | Target semantics. |
| `cozo-lib-dotnet/src/Om.Core/Logic/TypeLogic.cs` | .NET type definitions, alias resolution, hierarchy, and attribute definition merging. | Main implementation target. |
| `cozo-lib-dotnet/src/Om.Core/Logic/EntityLogic.cs` | .NET property validation, writes, reads, entity views, and type queries. | Alias and validity target. |
| `codument/archive/2026-02-28-add-type-hierarchy/spec.md` | Archived Bun type hierarchy behavior spec. | Normative type-system semantics. |

## Codebase Knowledge

- `om_mixin` stores mixin definitions, while `om_type_mixin` links types to mixins.
- Mixin attributes are stored in `om_attr_def` under the mixin name.
- Effective attributes should merge mixins first, then far ancestors, near ancestors, and self.
- Bun treats mixin attributes as reusable attributes, not as subtype parents.

## Domain Knowledge

- This track works in the kernel layer above raw Cozo facts and below higher ontology/rule features.
- Public C# APIs can be idiomatic `Async` methods while preserving stored-relation semantics.

## Terms

| Term | Meaning |
|------|---------|
| Effective attribute definition | The merged attribute definition visible for a type after mixin, ancestor, and self definitions are applied. |
| Tightening | Changing an inherited optional attribute to required without changing its value type. |
| Alias fallback | Reading legacy rows stored under old alias names when canonical rows are absent. |
