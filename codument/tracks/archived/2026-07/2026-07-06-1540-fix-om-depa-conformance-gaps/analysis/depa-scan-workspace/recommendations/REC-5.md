# REC-5 · EffectApiBuiltins 提升为公开契约（P2）

- 维度：分层（capsule internals 隔离）；裁定：GAP（轻）。
- 证据：EffectApiBuiltins.cs:7/15/20（internal record EffectApiRule + internal static class）被 Om.Depa 引用（DepaEffectCatalog.cs:2,18）；单程序集下 internal 跨目录可见，capsule 边界仅靠约定。
- 改什么（二选一，倾向 a）：
  a. 把 `EffectApiRule` 与 `EffectApiBuiltins`（或仅一个只读 `IReadOnlyList<EffectApiRule> BuiltinEffectApis` 属性）设为 public——观测层公开自己的效应词表契约，Om.Depa 继续经公开面投影。track decision #1 的依赖方向不动。
  b. 若不愿扩大公开面：保持现状并在 depa-map.json 的 capsule internals 豁免里登记（工具侧 V-L1/internals 规则可识别的 known-exception），零代码改动。
- 影响面：a 扩大 NuGet 公开 API 面（需一次 API review）；b 零改动。
