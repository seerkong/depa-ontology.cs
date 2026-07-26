# Knowledge

| Topic | Current understanding |
|---|---|
| Package boundary | `packages/Om.Scripting.Jint` references the root C# project and Jint; `Om.Core` does not reference the adapter. |
| Binding boundary | Exact manifest binding ids select script sources and produce the existing typed callback binding records. |
| Binding shape | The current `BehaviorCatalogCallbackSlot` determines the typed delegate emitted into `BehaviorCallbackBindingSet`; incompatible same-id reuse is rejected before import. |
| Runtime isolation | One fresh configured Jint engine per invocation; no cross-invocation globals. This is in-process limiting, not OS/process sandboxing. |
| Host boundary | Only explicitly named OM capabilities are projected; raw runtime/store/CLR objects are absent. |
| Effects | Host calls execute through the existing callback context so action transaction and rollback semantics remain owned by Om.Core. Parent-action host calls return mutation specs for explicit child composition rather than auto-applying them. |
| Errors | Compile, execute, timeout, cancellation, conversion and host failures retain binding identity and deterministic adapter codes. |
