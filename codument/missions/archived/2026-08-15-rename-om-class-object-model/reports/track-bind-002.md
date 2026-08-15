# Track Bind Report

Time: 2026-08-14T21:21:03Z

## Mission task

- `G2-T2`: Create and execute ontology relation/computed/operation behavior track.

## Bound track

- Real track id: `rename-ontology-operation-computedprop-contract`
- Project ref: `ontology-core`
- Track location: `codument/tracks/active/rename-ontology-operation-computedprop-contract/track.xml`

## Evidence

- Previous bound track `rename-ontology-om-class-object-contract` is marked completed and passed:
  - `dotnet run --project tests/Depa.Ontology.Tests/Depa.Ontology.Tests.csproj --no-restore`
  - `dotnet run --project examples/Depa.Ontology.ExampleServer.Tests/Depa.Ontology.ExampleServer.Tests.csproj --no-restore`
  - `codument validate rename-ontology-om-class-object-contract --strict`
- Remaining ontology-core legacy vocabulary belongs to the mission-approved second slice: relation, computed prop, operation/action, permission operation, and related behavior/interceptor surfaces.

## Applied change

- Marked `G2-T1` as `DONE`.
- Marked `G2-T2` as `ACTIVE`.
- Replaced the older TrackLink attribute shape with the current bound form:
  `state="bound" id="rename-ontology-operation-computedprop-contract" project-ref="ontology-core" required="true"`.
