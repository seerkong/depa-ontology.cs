# Decision Tree

## Root Question

How can the .NET OM schema-evolution surface reach the agreed Bun-compatible
reliability contract without breaking existing C# callers or narrowing its
broader schema snapshots?

## Severity

`auto`: the user authorized autonomous mission execution and previously chose
D2=B and D4.

## Resolved Frontier

- D2=B: add strict-by-default, atomic V2 APIs; retain obsolete compatibility
  wrappers; initialize legacy schemas in detect-only mode unless an explicit
  upgrade or opt-in is requested.
- D4: retain the C# snapshot's behavior-definition coverage and add structured
  diff rather than replacing it with Bun's narrower shape.

## Assumptions

- The existing public migration and rollback methods retain their current
  observable defaults during the migration window.
- A Cozo transaction is the required all-or-nothing boundary for preflight,
  application, snapshot/version state, and migration history.
