# incidents — 事故现场与裁定

## I-1 · V-F2 五条（工具 GAP）真伪裁定：**误报（heuristic 标注错位，非代码违规）**

工具链路复盘：
- 标注来源：DepaScanPipeline.cs:233-237 heuristic 把**名字含 "Runtime"/"Context" 的 record** 判为 depa_runtime_carrier（confidence 0.7）——OmMutationContext 因名中 "Context" 命中。
- 检测规则：DepaViolationDetectors.cs:503-544 DetectF2 对 carrier 的每个非 accessor/ctor 且有 outgoing CALLS 的方法记 GAP。OmMutationContext 的 5 个方法各有 1 条 CALLS → 5 条 hit。

逐条核实（CozoOmRuntime.cs）：

| 行 | 方法 | 方法体 | 业务逻辑？ |
|----|------|--------|-----------|
| :121-122 | GetPropertyAsync | `=> Logic.EntityLogic.GetPropertyAsync(Runtime, EntityId, attrName, ct)` | 否，单表达式委托 |
| :124-125 | GetPropertyAsOfAsync | `=> Logic.EntityLogic.GetPropertyAsOfAsync(Runtime, …)` | 否 |
| :127-128 | SetPropertyAsync | `=> Logic.EntityLogic.SetPropertyAsync(Runtime, new SetPropertyInput(…), ct)` | 否 |
| :130-131 | LinkEntitiesAsync | `=> Logic.RelationLogic.LinkEntitiesAsync(Runtime, new LinkEntitiesInput(…), ct)` | 否 |
| :133-134 | GetNeighborsAsync | `=> Logic.RelationLogic.GetNeighborsAsync(Runtime, …)` | 否 |

裁定依据（runtime-paradigm 不变量①：runtime/context 数据载体不应带业务逻辑方法）：
1. **OmMutationContext 不是 runtime 载体**。真正的 runtime 载体是 CozoOmRuntime（CozoOmRuntime.cs:8-11），三字段纯数据 record、零方法——不变量①对它成立。OmMutationContext(:119) 是交给用户注册的 mutation/action 回调的**作用域上下文 facade**（由 ConstraintLogic.cs:137 `new OmMutationContext(runtime, id, typeName)` 构造、传入 executor），属公开扩展 API 面，与 CozoOm facade 同一角色。
2. **五个方法零业务逻辑**：全部是单表达式转发到 Logic 外部静态函数、且以 Runtime 为首参——恰是判据要求的"逻辑=以 runtime 为首参的外部函数"形态。逻辑本来就在载体外，facade 只是把它拼回给回调作者用。
3. 结论：真违规判据（载体内写业务分支/IO）不成立。**是标注问题**：本仓库的 depa-map.json 未声明 runtimeCarrierTypes，heuristic 按名猜错了 carrier。

整改（标注侧，非代码侧）：在本仓库 depa-map.json 声明 `runtimeCarrierTypes: ["CozoOmRuntime", "CozoOmOptions"]`（config 通道 confidence 1.0，DepaScanPipeline.cs:194-199 优先于 heuristic，OmMutationContext 不再被猜为 carrier）。可选加固见 recommendations/REC-4。

## I-2 · Om.Depa 直删 om_entity + 孤儿 om_property：**GAP（真）**

- DepaViolationDetectors.cs:726-732 过期违规用原生 `:rm om_entity {id}` 直删——绕过 Om.Core（om_entity owner），Om.Depa 成为第二写入者。
- 且只删实体行：:663-673 SetPropertyAsync 写入的 11 个属性（rule_id/verdict/…/evidence_json）残留 om_property（时态 @ NOW），孤儿数据永不回收；LoadTypeAsync(:861-868) 靠 join om_entity 掩盖。
- 根因：CozoOm 无 DeleteEntityAsync 公开 API（CozoOm.cs 全文无删除）。

## I-3 · Om.Depa 管线内直接文件 IO：**GAP（轻，真）**

- DepaConfigModels.cs:55-60（DepaMapConfig.Load：File.Exists + File.ReadAllText）、DepaEffectCatalog.cs:27-32（LoadUserEffects 同型），由核心管线 DepaScanPipeline.ScanAsync 内部调用（:52、:413-414）。
- DepaScanOptions（DepaScanModels.cs:11）携带 MapPath/EffectsPath 字符串——config 携带路径、core 自己发现依赖并做 IO，违反"core 不直接 IO / 依赖外层加载一次下传"。
- 对"CozoDbOmStore 唯一副作用出口"承诺的裁定：**db 副作用意义上仍成立**（三 capsule 所有 db 访问都经 ICozoOmStore，无第二条 db 通道）；文件读取是后来引入的第二类副作用，未走契约。

## I-4 · Om.CodeKnowledge 直取 DateTimeOffset.UtcNow：**GAP（轻，真）**

- CozoOmCodeKnowledgeExtensions.cs:169 `DateTimeOffset.UtcNow.ToString("O")` 写 ck_meta.indexed_at。
- Om.Core 已把时间显式化进 runtime（CozoOmOptions.TimeProvider，CozoOmRuntime.cs:16；EntityLogic.cs:60/64 用 runtime.Options.TimeProvider）——同库两种时钟纪律，CodeKnowledge 侧未走 runtime。

## I-5 · Om.Depa 引用 Om.CodeKnowledge internal 类型：**GAP（轻，真；方向合规、可见性越界）**

- DepaEffectCatalog.cs:2 `using Cozo.DotNet.Om.CodeKnowledge`、:18 投影 `EffectApiBuiltins.Rules`；EffectApiBuiltins.cs:15 与 EffectApiRule(:7) 均 `internal`。
- 单程序集下编译可过，但 capsule 口径下这是"跨 capsule 触对方非公开面"（capsule-protocol §5 禁止项）。依赖**方向**单向合规（注释自证 "Om.Depa → Om.CodeKnowledge (track decision #1)"）。
