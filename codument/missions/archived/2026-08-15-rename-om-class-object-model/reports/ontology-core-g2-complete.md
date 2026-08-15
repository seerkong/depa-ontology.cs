# Ontology Core G2 Completion Report

Time: 2026-08-14T22:39:00Z

## Completed nodes

- `G2-T1`: `rename-ontology-om-class-object-contract`
- `G2-T2`: `rename-ontology-operation-computedprop-contract`
- `G2-T3`: ontology-core legacy vocabulary verification

## Evidence

- `codument validate rename-ontology-om-class-object-contract --strict` passed.
- `codument validate rename-ontology-operation-computedprop-contract --strict` passed.
- `dotnet build tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore -v minimal` passed.
- `dotnet run --project tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore` passed with `Depa.Ontology integration tests passed.`
- `dotnet build examples/Depa.Ontology.ExampleServer/Depa.Ontology.ExampleServer.csproj --no-restore -v minimal` passed.
- `dotnet run --project examples/Depa.Ontology.ExampleServer.Tests/Depa.Ontology.ExampleServer.Tests.csproj --no-restore` passed with `Example server HTTP contract tests passed.`
- Targeted scans over `src`, `tests`, and `examples` found only the intentional old public API deny-list in `tests/Depa.Ontology.Tests/PublicSurfaceParityFixtures.cs`.

## Decision

Ontology-core redesign is complete enough for dependent knowledge-base migration to begin.
