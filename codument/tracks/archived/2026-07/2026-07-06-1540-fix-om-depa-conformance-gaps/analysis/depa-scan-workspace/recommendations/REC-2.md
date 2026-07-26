# REC-2 · depa 配置加载上提到入口边界（P1）

- 维度：Effect + 分层（core 不直接 IO）；裁定：GAP（轻）。
- 证据：DepaScanPipeline.cs:52（DepaMapConfig.Load）、:413-414（DepaEffectCatalog.LoadUserEffects）在核心管线内做 File.ReadAllText（DepaConfigModels.cs:55-60、DepaEffectCatalog.cs:27-32）；DepaScanOptions 只携带路径（DepaScanModels.cs:11-15）。
- 改什么：
  1. CozoOmDepaExtensions.DepaScanAsync（CozoOmDepaExtensions.cs:44-48）在入口先解析 depa-map.json / depa-effects.json，DepaScanPipeline.ScanAsync 签名改吃 `DepaMapConfig` + `IReadOnlyList<DepaEffectRule>`（internal 签名，随意改）。
  2. 可选：公开一个 `DepaScanAsync(om, DepaMapConfig map, IReadOnlyList<DepaEffectRule> effects, …)` overload，便于测试注入零文件依赖。
  3. DepaOntologySchema.SyncEffectApisAsync 同步改吃已解析 effects。
- 影响面：不破坏现有公开 API（路径版保留为薄壳）；tests 中直接调 pipeline 的用例需随 internal 签名微调。
