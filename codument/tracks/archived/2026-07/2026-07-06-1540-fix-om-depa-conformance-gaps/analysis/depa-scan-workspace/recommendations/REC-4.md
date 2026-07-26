# REC-4 · V-F2 误报消除：标注为主、代码为辅（P1 标注侧 / P2 可选代码侧）

- 维度：标注/工具（V-F2 heuristic）；裁定：**误报**（五条全部；详证 inventory/incidents.md I-1）。
- 必做（标注，零代码改动）：为本仓库扫描提供 depa-map.json，显式声明 `"runtimeCarrierTypes": ["CozoOmRuntime", "CozoOmOptions"]`——config 通道（confidence 1.0）先判，heuristic（DepaScanPipeline.cs:233-237 按名含 Runtime/Context 猜）不再触碰 OmMutationContext/OmActionContext/OmValidationContext/OmComputedContext。
- 可选（代码，纯形态、零行为变化）：把 OmMutationContext 的 5 个便捷方法（CozoOmRuntime.cs:121-134）改为 `OmMutationContextExtensions` 扩展方法——record 变纯数据，任何 heuristic 天然干净；调用方源码兼容（`ctx.SetPropertyAsync(...)` 写法不变），但**二进制不兼容**（instance→static），若有外部已编译消费者需评估。
- 工具侧 backlog 建议（超本仓库范围，见 backlog.md B-3）：V-F2 检测器可加"纯委托豁免"——方法体只有一条 CALLS 且目标首参为 runtime 载体类型时降为 INFO，从根上减少此类误报。
