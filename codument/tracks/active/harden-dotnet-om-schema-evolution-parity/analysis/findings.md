# Findings

## Found Facts

- `SchemaMigrationSpec.Strict` defaults to `false` in
  `cozo-lib-dotnet/src/Om.Core/Inputs/OmInputs.cs`.
- `SchemaLogic.RollbackSchemaAsync` accepts `strict` but does not use it, clears
  relations before rebuilding them, and returns no structured result.
- `ApplySchemaMigrationAsync` applies individual steps before later snapshot,
  state and history writes, without one transaction boundary.
- `SchemaDiff` holds whole `JsonElement` added/removed/changed payloads rather
  than keyed per-definition changes.
- Bun's migration and rollback suites define strict/force diagnostics and
  transactional expectations.

## Constraints

- D2=B requires additive V2 APIs and obsolete compatibility wrappers.
- D4 preserves the broader C# snapshot including behavior metadata.
- Legacy relation detection must not silently mutate stored data by default.

## Conclusions

- Characterization tests must precede the V2 implementation.
- V2 preflight and application must share the same normalized plan and one
  transaction boundary.
