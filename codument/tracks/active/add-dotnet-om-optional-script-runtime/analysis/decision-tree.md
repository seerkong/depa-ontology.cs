# Decision Tree

## Root Question

How should the optional JavaScript runtime bind portable behavior manifests without weakening the established C# OM runtime and package boundaries?

## Severity

`auto`: the mission is already approved for implementation and its D3/D4 decisions close the user-decision frontier.

## Resolved Decisions

- Use in-process Jint in a package outside `Om.Core`.
- Preserve typed async delegates, behavior binding identities, transaction boundaries and metadata/delegate separation.
- Keep canonical behavior JSON as the only manifest truth; do not add a second serialized script manifest in this track.
- Require cancellation, wall-clock timeout, statement and recursion limits, Jint-supported memory limits, host allowlisting, stable error mapping and no ambient filesystem/network access.

## Conservative Assumptions

- Script sources are supplied programmatically by exact binding id and never persisted by `Om.Core`.
- The provider produces the existing `BehaviorCallbackBindingSet`; the existing import pipeline owns preflight, persistence, readiness and atomic publication.
- Exact binding id lookup is ordinal string equality, and the current catalog slot determines the generated typed delegate shape. Reusing one id across incompatible shapes is a provider diagnostic, not a user decision.
- A fresh engine is created for each callback invocation. Mutable JavaScript globals are not shared across entities, threads or requests.
- Host access is capability based and behavior-kind aware. Raw `CozoOmRuntime`, stores, CLR type access and arbitrary host objects are never exposed.
- Jint is pinned in the adapter package; the current official NuGet version selected for implementation is 4.13.0 as verified on 2026-07-17.
- In-process limits are not an OS/process sandbox. The track must not claim safety for hostile untrusted code without separate process isolation.

## Open Frontier

None. Any proposal to persist source code, add a new script serialization format, expose arbitrary CLR objects, share mutable engines or move Jint into `Om.Core` reopens user decision D3/D4.
