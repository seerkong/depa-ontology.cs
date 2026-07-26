# boundary — 事实源边界（导航 + 问题清单）

> 目录职责：holds=骨架观测、①问题清单、事实定级/唯一写入者/反写红灯；excludes=逐行证据盘点（去 inventory/）、处置（去 convergence/）；tier=dated（2026-07-06 快照，HEAD 代码）。

## 目标骨架

- 单程序集：三个 capsule 都编进根 `Cozo.DotNet.csproj`（cozo-lib-dotnet/Cozo.DotNet.csproj），capsule 边界=目录约定 + `internal` 修饰符（跨目录仍可见）。
- Om.Core（对象模型核）：分层齐备 — `Contracts/`（ICozoOmStore.cs:5 契约 + Models）、`Inputs/`（OmInputs.cs 纯 input record）、`Logic/`（6 个 public static Logic 类，首参 runtime）、`Runtime/`（CozoOmRuntime.cs:8 三字段纯数据 record）、`Support/`（CozoDbOmStore.cs:7 effect 实现 + CozoScriptBuilder.cs:3 纯 helper）、`Internals/`（JsonRows/OmConvert，无外部 import——grep 全仓仅 Om.Core 内引用）。入口 facade：CozoOm.cs:11。
- Om.CodeKnowledge（观测层）：平铺目录，ck_* schema + 索引/查询扩展（CozoOmCodeKnowledgeExtensions.cs:8）、派生投影（CodeGraphProjections/ProcessExtraction/DeepImpact）、效应白名单（EffectApiBuiltins.cs:15，internal）。
- Om.Depa（判断/派生层）：平铺目录，公开面仅 CozoOmDepaExtensions.cs:8 的 4 个扩展方法 + models；管线/检测器/schema 全 internal。
- 依赖方向：Om.Depa → Om.CodeKnowledge（DepaEffectCatalog.cs:2）→ Om.Core；Om.CodeKnowledge → Om.Query/Datalog.Cozo（CozoOmCodeKnowledgeExtensions.cs:2-4）。无回边。

## 问题清单（环节①）

1. V-F2×5：OmMutationContext 带 5 个方法（CozoOmRuntime.cs:121/124/127/130/133）——它是 runtime 载体还是回调上下文 facade？方法体是业务还是纯委托？→ 裁定见 inventory/incidents.md I-1。
2. Om.Depa 过期违规清理直接 `:rm om_entity`（DepaViolationDetectors.cs:726-732），绕过 Om.Core Logic 层——om_entity 的唯一写入者是谁？孤儿 om_property 是否残留？→ I-2。
3. Om.Depa 管线内直接 File.ReadAllText（DepaConfigModels.cs:60、DepaEffectCatalog.cs:32，由 DepaScanPipeline.cs:52/414 调用）——"CozoDbOmStore 唯一副作用出口"承诺是否被文件 IO 打破？→ I-3。
4. Om.CodeKnowledge 索引尾用 `DateTimeOffset.UtcNow`（CozoOmCodeKnowledgeExtensions.cs:169）而 Om.Core 已有 TimeProvider（CozoOmRuntime.cs:16）——非确定性未走 runtime？→ I-4。
5. Om.Depa 引用 Om.CodeKnowledge 的 internal 类型 EffectApiBuiltins（DepaEffectCatalog.cs:18 ← EffectApiBuiltins.cs:15 internal）——跨 capsule 触非公开面？→ I-5。
6. ck_* / depa_* 两层的唯一写入者与反写：index 时 category 预分类（CozoOmCodeKnowledgeExtensions.cs:146-150），scan 时是否回写观测行？→ fact-grades.md / single-writer.md。
7. CozoOm.AddInterceptorAsync 先注册内存 registry 再写 db（CozoOm.cs:179-189）——失败时内存/db 漂移？→ backlog（B-1）。
