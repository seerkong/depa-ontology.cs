# Knowledge Context

## Source Notes

| Source | Summary | Relevance |
|---|---|---|
| `cozo-lib-bun/cozo-om.js` | Reference permission path, ABAC, field visibility and explanation semantics. | Observable parity target. |
| `src/Om.Core/Logic/ConstraintLogic.cs` | Current C# permission seed/write/read/evaluation flow. | Implementation owner. |
| `src/Om.Core/Contracts/Models/OmModels.cs` | Typed C# access input/result contracts. | Public API evolution surface. |

## Terms

| Term | Meaning |
|---|---|
| Path witness | Ordered subject-to-resource relation hops proving one declared policy path. |
| ABAC | Attribute-based policy predicates over subject/resource types and properties. |
| Field visibility | Per-field result such as `hidden` that accompanies an allowed read. |
| Fail closed | Missing entities, malformed paths/refs, unsupported operators or unmatched predicates do not grant access. |
