# G9 Scope Attribution

This track runs in a shared working tree while the parent mission executes G3-G8
and the ontology/wiki tracks. The unrelated modified files must remain intact and
are not evidence of G9 work.

## Attributable G9 Files

- `cozo-lib-dotnet/src/Om.Core/Contracts/Models/OmModels.cs`: `TypeParentPatch`
  and `DefineTypePatchInput` public contracts.
- `cozo-lib-dotnet/src/Om.Core/Inputs/OmInputs.cs`: public input namespace
  compatibility imports used by the patch facade.
- `cozo-lib-dotnet/src/Om.Core/CozoOm.cs`: typed parent, constraint,
  traversal, and value-inference facade methods.
- `cozo-lib-dotnet/src/Om.Core/Logic/TypeLogic.cs`: Keep/Set/Clear behavior.
- `cozo-lib-dotnet/src/Om.Core/Logic/ConstraintLogic.cs`: public constraint
  validation delegation and existing registration guardrails.
- `cozo-lib-dotnet/src/Om.Core/Logic/RelationLogic.cs`: public traversal
  delegation.
- `cozo-lib-dotnet/src/Om.Core/Internals/OmConvert.cs`: value inference,
  including `null`/JSON null as `Unknown`.
- `cozo-lib-dotnet/tests/PublicSurfaceParityFixtures.cs`: focused G9 behavior
  cases and no-ghost registration assertions.
- Files under this track's `reports/` and this attribution note.

## Explicitly Unrelated

Changes in `cozo-lib-bun`, `cozo-lib-dotnet-llm-wiki`, optional script and
portability packages, permission/schema/existential/action tracks, and other
mission tracks are concurrent work and remain outside G9. The shared worktree
must not be cleaned or reverted to create an artificial attribution boundary.

## Review Rule

G9 direction checks evaluate the listed public-surface symbols and focused
fixture. They may mention unrelated files as environmental context, but must not
classify those files as G9 scope violations.
