# package-boundaries — capsule 判定（测量尺 2 §6）+ DEPA 七点打分

## 依赖方向

- Om.Depa → Om.CodeKnowledge（DepaEffectCatalog.cs:2）→ Om.Core（CozoOmCodeKnowledgeExtensions.cs:3）。无回边 ✅。
- Om.Depa 对 ck_* 只读、经 db schema 契约（DepaScanPipeline.cs:468-575 全只读 try/catch），不 import CodeKnowledge 查询 API——schema 即契约，可接受。
- 唯一越界：EffectApiBuiltins internal 被 Om.Depa 引用（I-5）。

## capsule 六维判定

| 维度 | Om.Core | Om.CodeKnowledge | Om.Depa |
|------|---------|------------------|---------|
| 目录布局 | PASS（Contracts/Inputs/Logic/Runtime/Support/Internals 各就位；小疵：Support/ 混装 effect 实现 CozoDbOmStore.cs:7 与纯 helper CozoScriptBuilder.cs:3） | PASS（平铺但单一职责文件；无 Internals 目录，靠 internal 修饰） | PASS（平铺；public 面极窄） |
| 入口唯一 | PASS*（CozoOm.cs:11 facade + Logic 类亦 public——双入口是 DEPA 的"logic=外部函数"设计使然，facade 纯委托无漂移） | PASS（扩展方法族，均以 CozoOm 为首参） | PASS（CozoOmDepaExtensions.cs:8 四方法即全部公开面，注释自证） |
| 类型纯净 | PASS（Contracts/Models 对外，Internals 不泄漏——OmQueryResult 等均在 Contracts） | PASS（CodeKnowledgeModels.cs 公开契约） | PASS（DepaScanModels/DepaReportModels 公开，管线类型 internal） |
| adapter 注册 | PASS（CozoOmRegistry 注册表 + TryGet 显式 bool + 未知 mutation 显式 throw ConstraintLogic.cs:142-145） | 不适用（无多策略轴） | PASS（检测规则表 DetectionRuleIds + BLOCKED 不猜，DepaViolationDetectors.cs:38-41） |
| internals 隔离 | PASS（grep 全仓：Cozo.DotNet.Om.Internals 无 capsule 外 import） | GAP（EffectApiBuiltins internal 被 Om.Depa 引用，EffectApiBuiltins.cs:15 ← DepaEffectCatalog.cs:18） | PASS（internal 类型无外部引用） |
| 依赖指向 | PASS（不依赖上层） | PASS（→ Om.Core/Om.Query 单向） | GAP 同上一格（方向合规、可见性越界）；另 :rm om_entity 绕 Om.Core 写面（I-2） |

## DEPA 七点打分（depa-conformance）

| 点 | Om.Core | Om.CodeKnowledge | Om.Depa |
|----|---------|------------------|---------|
| ① Runtime 显式化 | 符合 | 部分（UtcNow 直取，I-4） | 部分（File IO+路径自发现，I-3） |
| ② Data | 符合 | 符合（观测层单写、增量删除单事务） | 部分（孤儿 om_property + 绕层删，I-2） |
| ③ Effect | 符合（IO 全经 ICozoOmStore） | 符合 | 部分（文件读未走契约，I-3） |
| ④ Processor | 符合（注册表+显式报错） | 符合 | 符合（BLOCKED 不猜） |
| ⑤ Actor | 不适用（纯异步顺序，无并发协作） | 不适用 | 不适用 |
| ⑥ 过度设计 | 符合 | 符合 | 符合 |
| ⑦ Vendor 原语 | 符合（TimeProvider/record/ValueTask 全 vendor） | 符合 | 符合 |
| **得分** | **6/6 高** | **5/6 高** | **3.5/6 混合偏高** |
