# fact-grades — 关键数据节点 7 级定级

| 节点 | 定级 | 依据（path:line） |
|------|------|-------------------|
| ck_repo/ck_file/ck_symbol/ck_edge/ck_doc_block/ck_entry_point/ck_external_call/ck_owner/ck_diagnostic | 2 `domain_canonical_event`（观测事实，索引批次落行，可由源码重扫重建） | 写入 CozoOmCodeKnowledgeExtensions.cs:30-162；删除（增量）同文件 :197-293，单事务 |
| ck_community/ck_member/ck_process/ck_process_step | 6 `derived_projection_cache`（`:replace` 全量重算，可重建） | CodeGraphProjections.cs（:replace ck_community/ck_member）、ProcessExtraction.cs（:replace ck_process*） |
| ck_meta.indexed_at | 4 journal 性质旁路元信息 | CozoOmCodeKnowledgeExtensions.cs:164-170 |
| depa_*（capsule/contract/impl/fact_source/runtime_carrier/entry/violation…） | 6 `derived_projection_cache`（由 ck_* + depa-map.json 判断合成，重扫幂等重建） | DepaScanPipeline.cs:63-96 upsert、DepaViolationDetectors.cs:662-697 物化、:712-733 过期 |
| depa-map.json / depa-effects.json | 外部 config 事实（1 级 config truth，用户拥有） | DepaConfigModels.cs:53-60、DepaEffectCatalog.cs:25-33 |
| CozoOmRegistry（内存 validators/mutations/actions/interceptors） | 3 `runtime_control_fact`（应用作用域可变控制面，进程内唯一） | CozoOmRuntime.cs:19-105 |
| om_entity/om_property/om_edge（OM 本体） | 1/2 权威事实（Om.Core 拥有，时态 @ NOW） | EntityLogic.cs:16-101 写路径 |

交叉校验：
- 反写检查：depa_scan 对 ck_external_call 的 category **不回写**观测行——scan 时用合并白名单在内存重匹配（DepaScanPipeline.cs:413-416），注释明示 "never rewrites these observation rows"（CozoOmCodeKnowledgeExtensions.cs:144-145）。无 6→2 反写。✅
- 唯一红灯：depa_violation 过期时 `:rm om_entity` 只删实体行、不删其 om_property 行（DepaViolationDetectors.cs:726-732 对照 :663-673 写入的 11 个属性）→ 孤儿属性残留，见 backwrite-risks.md R-1。
