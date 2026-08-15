# Depa.Ontology Example Server

Private ASP.NET Core example API for `depa-ontology.ts/packages/example-browser`; it is not a NuGet package.

The demo table contract follows the renamed OM language: `Class 定义`,
`Field 定义`, `RelationDef 定义`, `ComputedProp 定义`, `Operation 定义`,
`Object 数据`, `FieldValue 数据`, and `RelationLink 数据` use `className`,
`fieldName`, `valueKind`, `relationName`, `fromClass`, `toClass`,
`computedPropName`, `operationName`, `objectId`, `fromObjectId`, `toObjectId`,
and `payload` columns. Operation-oriented examples and governance endpoints use
permission `operation` language. Use it with an example-browser version that
speaks the same Class/Object/Field/FieldValue, RelationDef/RelationLink,
ComputedProp, and Operation contract.

## Run with `example-browser`

The browser defaults to `http://127.0.0.1:4175`, so start this server on that
address from this repository:

```bash
dotnet run --project examples/Depa.Ontology.ExampleServer --urls http://127.0.0.1:4175
```

In a second terminal, start the browser from the adjacent TypeScript repository:

```bash
cd ../depa-ontology.ts
VITE_API_BASE=http://127.0.0.1:4175 npm run dev --workspace=depa-example-browser
```

`VITE_API_BASE` may point at another server address, but it must be set before
Vite starts.  Omitting it uses the browser's same `127.0.0.1:4175` default.

The default CORS policy permits only `http://localhost:4174` and
`http://127.0.0.1:4174`, which are the browser dev-server origins. Add an
additional trusted development origin without replacing those defaults, for
example:

```bash
Cors__AllowedOrigins__0=http://localhost:5173 \
  dotnet run --project examples/Depa.Ontology.ExampleServer --urls http://127.0.0.1:4175
```

The policy never defaults to an arbitrary origin.

## Local Cozo prerequisite

`src/Depa.Ontology/Depa.Ontology.csproj` intentionally references the sibling
source project `../../../cozo/depa-cozo-csharp/Depa.Cozo.csproj` while the
binding is being migrated. Ensure that sibling checkout is present and that its
native `cozo_c` library is loadable by .NET; `/api/run` and the governance
endpoints create real in-memory Cozo databases. Package publishing must replace
this development project reference with a released `Depa.Cozo` package that
contains the same transaction API and native runtime assets.

## Verify

Run its HTTP contract checks with:

```bash
dotnet build Depa.Ontology.slnx --configuration Release --no-restore
dotnet run --project examples/Depa.Ontology.ExampleServer.Tests/Depa.Ontology.ExampleServer.Tests.csproj --configuration Release --no-build
```
