# P2 Coding Attractor Round 2

## Result

GAP

## Finding

An unresolved computed definition can be bypassed by a same-name stored property. Direct and as-of property reads return the stored value before entering computed readiness resolution; current and as-of entity views skip computed resolution when their property map already contains the attribute.

## Evidence

- `cozo-lib-dotnet/src/Om.Core/Logic/EntityLogic.cs:486`
- `cozo-lib-dotnet/src/Om.Core/Logic/EntityLogic.cs:309`
- `cozo-lib-dotnet/tests/Program.cs:730`

The existing fail-closed tests use an entity with no stored value for the computed attribute, so they do not exercise these early-return and skip branches.

## Required Repair

All four direct/view and current/as-of computed read surfaces must resolve the effective computed definition and enforce exact binding readiness before using stored-value precedence. Ready or unbound definitions retain stored-value precedence; the compute callback runs only when no stored value exists.

## Scope Check

The reviewer found no additional P2 gap in transaction/publish compensation, constraint type-slot preflight, YAML safety, or dependency isolation. P3's broader restart and fault matrices were not treated as P2 failures.
