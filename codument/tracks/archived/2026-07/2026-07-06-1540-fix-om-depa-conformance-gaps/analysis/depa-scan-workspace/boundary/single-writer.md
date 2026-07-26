# single-writer — 唯一写入者判定

| 数据块 | 唯一写入者候选 | 判定 | 证据 |
|--------|----------------|------|------|
| ck_*（观测层 11 表） | Om.CodeKnowledge | ✅ 唯一（capsule 内三处写者均属同 capsule：批量索引 CozoOmCodeKnowledgeExtensions.cs、投影 CodeGraphProjections.cs、流程抽取 ProcessExtraction.cs；Om.Depa 只读——DepaScanPipeline.cs:468-575 全部 try/catch 只读查询） | grep `:put/:rm/:replace ck_` 全命中 src/Om.CodeKnowledge |
| depa_*（判断层实体/关系） | Om.Depa | ✅ 唯一（DepaScanPipeline + DepaViolationDetectors + DepaOntologySchema；Om.CodeKnowledge 仅注释提及 depa_，无写） | grep depa_ Om.CodeKnowledge 仅 2 处注释 |
| om_entity/om_property/om_edge | Om.Core EntityLogic/RelationLogic | ⚠️ 混乱一处：Om.Depa 用原生脚本 `:rm om_entity {id}` 直删（DepaViolationDetectors.cs:726-732），成为 om_entity 关系的第二个（绕层）写入者。根因：CozoOm 无 DeleteEntityAsync 公开 API（CozoOm.cs 全文无删除实体方法） |
| CozoOmRegistry | 入口装配方（应用 bootstrap 经 CozoOm.Register*） | ✅ 唯一（CozoOm.cs:139-189 全部经 Runtime.Registry） |
| effect 白名单（内建表） | Om.CodeKnowledge EffectApiBuiltins.Rules（readonly） | ✅ 编译期常量；Om.Depa 只投影（DepaEffectCatalog.cs:17-18） |
