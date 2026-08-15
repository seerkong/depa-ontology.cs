# Implementation Report

Track: `rename-ontology-om-class-object-contract`
Time: 2026-08-14T21:17:29Z

## Summary

This track completed the first breaking ontology OM rename slice:

- Type -> Class
- Entity -> Object
- Attribute -> Field
- durable Property -> FieldValue

The implementation updates the public `CozoOm` API, public model DTOs, schema creation, schema evolution fixtures, current-slice Cozo table and column names, logic queries, tests, example server usage, and current example documentation.

RelationLink, ComputedProp, Operation, scripting-context, and permission/governance action vocabulary remain intentionally reserved for the next mission track.

## Modified Areas

- Ontology core public API and public DTOs
- Ontology core schema initialization, snapshots, diff, rollback, and migration steps
- Object/class/field-value logic paths
- Existential rule object/field lookup paths touched by this slice
- Ontology integration tests and parity fixtures
- Example server demo seed, schema apply example, and README
- Current example-server behavior wording

## Verification Commands

- `dotnet build tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore -v minimal`
  - Result: passed with 0 warnings and 0 errors.
- `dotnet run --project tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore`
  - Result: `Depa.Ontology integration tests passed.`
- `dotnet build examples/Depa.Ontology.ExampleServer/Depa.Ontology.ExampleServer.csproj --no-restore -v minimal`
  - Result: passed with 0 warnings and 0 errors.
- `dotnet build examples/Depa.Ontology.ExampleServer.Tests/Depa.Ontology.ExampleServer.Tests.csproj --no-restore -v minimal`
  - Result: passed with 0 warnings and 0 errors.
- `dotnet run --project examples/Depa.Ontology.ExampleServer.Tests/Depa.Ontology.ExampleServer.Tests.csproj --no-restore`
  - Result: `Example server HTTP contract tests passed.`
- `dotnet build Depa.Ontology.slnx --no-restore -v minimal`
  - Result: passed with 0 warnings and 0 errors.
- `codument validate rename-ontology-om-class-object-contract --strict`
  - Result: `track.xml OK + 1 behavior delta(s)`.
- `xmllint --noout` on track XML, behavior delta XML, and linked mission XML
  - Result: passed.

## Scan Results

Public `CozoOm` scan for old current-slice public methods returned no hits.

Storage scan for old current-slice tables and columns returned no required current-slice hits. Remaining hits are tied to later ComputedProp or Operation/governance storage:

- `om_computed_def`
- `type_name`
- `attr_name`

Source/test/example scan for old current-slice API and JSON/demo table names returned no hits in example server and example server tests. Remaining ontology test hits are callback or scripting-context terms such as `ctx.GetPropertyAsync`, `ctx.SetPropertyAsync`, and `ctx.entityId`; these are explicitly reserved for the later ComputedProp/Operation/scripting-context track.

## GapLoop Hook Note

The P4 phase declares a `cdt:GapLoop` hook. The required fresh subagent collaboration tool surface was not callable in this runtime turn, so a protocol-perfect fresh independent GapLoop round was not executed. This report records the available verification evidence and the boundary. The track has still been validated by real builds, runtime tests, strict Codument validation, XML validation, and targeted scans.

## Remaining Mission Work

The next mission slice must handle:

- Edge -> RelationLink
- Computed definition -> ComputedProp
- Action -> Operation
- mutation/query/operation composition vocabulary
- scripting and callback context naming
- permission operation naming
- knowledge-base dependent project and its skills
