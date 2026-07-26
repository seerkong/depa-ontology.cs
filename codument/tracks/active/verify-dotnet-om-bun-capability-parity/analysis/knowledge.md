# Knowledge Context

## Source Notes

| Source | Summary | Relevance |
|--------|---------|-----------|
| Mission proposal | Defines observable parity and final success criteria. | Verification target |
| Mission evidence | Defines G01-G14 original gaps and required outcomes. | Matrix rows |
| Mission decisions | Freezes D0-D5 and D3a/D3b. | Intentional differences |
| Linked tracks | Carry implementation and focused verification evidence. | Actual state |
| Bun and .NET suites | Executable behavior evidence. | Final gate |

## Codebase Knowledge

- Bun OM tests run from `cozo-lib-bun` with the workspace Bun binary.
- C# OM tests run from `cozo-lib-dotnet`; focused fixtures use
  `COZO_OM_TEST_FOCUS`.
- Codument strict validation is best-effort for every bound track.

## Domain Knowledge

- Parity means matching domain effects, rejection behavior, temporal meaning,
  authorization decisions, diagnostics, and inheritance semantics.
- API spelling and package shape may differ where the observable contract is
  preserved.

## Terms

| Term | Meaning |
|------|---------|
| Paired case | Equivalent behavior scenario executable in both runtimes |
| Intentional difference | User-approved language or architecture difference |
| Evidence row | One G01-G14 mapping to implementation, tests, and decision |
