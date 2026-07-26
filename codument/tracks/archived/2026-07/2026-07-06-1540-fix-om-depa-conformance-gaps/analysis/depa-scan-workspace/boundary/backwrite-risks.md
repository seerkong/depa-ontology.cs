# backwrite-risks — 反写红灯

| # | 现象 | 级别流向 | 证据 | 裁定 |
|---|------|----------|------|------|
| R-1 | depa_violation 过期清理 `:rm om_entity` 只删实体行，:663-673 写入的 om_property 行（rule_id/verdict/evidence_json…共 11 属性）不删，孤儿属性永久残留（时态表 @ NOW 仍持值）；且绕过 Om.Core 写路径 | 6 级派生层直改 1 级权威关系（绕 owner） | DepaViolationDetectors.cs:726-732（删）对照 :662-697（写）；LoadTypeAsync :861-868 靠 join om_entity 掩盖孤儿 | GAP（真）——数据卫生 + 唯一写入者双违 |
| R-2 | ck_external_call.category 索引时预分类、扫描时重匹配 | 潜在 6→2 反写嫌疑 | CozoOmCodeKnowledgeExtensions.cs:146-150 + DepaScanPipeline.cs:413-416 | PASS——scan 明确不回写观测行 |
| R-3 | depa 派生实体经 om.UpsertEntityAsync 写入 OM 本体（与用户业务实体同库同表） | 6 级派生与 1 级权威同表共存（depa: 前缀隔离） | DepaScanPipeline.cs:66 id=`depa:{...}` | PASS（带前缀命名空间隔离，设计如此）；记 backlog B-2：无 schema 级隔离，用户实体撞 depa: 前缀无防护 |
