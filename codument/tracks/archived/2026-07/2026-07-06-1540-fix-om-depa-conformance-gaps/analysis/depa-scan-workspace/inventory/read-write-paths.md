# read-write-paths — live 读写路径

| # | 路径 | 判定 | 证据 |
|---|------|------|------|
| 1 | 索引写：CodeKnowledgeBatch → 单写事务 → ck_*（:put） | OK | CozoOmCodeKnowledgeExtensions.cs:26-172（BeginTransaction…Commit） |
| 2 | 增量删：RemoveFileFactsAsync 单事务、先边后点的删除顺序 | OK | CozoOmCodeKnowledgeExtensions.cs:211-293（注释明示 order matters） |
| 3 | depa_scan 读 ck_* → 写 depa_*（om API upsert，幂等） | OK | DepaScanPipeline.cs:63-96、:264-271 |
| 4 | 违规过期：UnlinkEntities（om API）+ `:rm om_entity`（原生） | RISK（I-2：绕层 + 孤儿 om_property） | DepaViolationDetectors.cs:720-733 |
| 5 | scan 内配置读：File.ReadAllText(depa-map.json / depa-effects.json) | RISK（I-3：core 内文件 IO） | DepaScanPipeline.cs:52,413-414 → DepaConfigModels.cs:60、DepaEffectCatalog.cs:32 |
| 6 | 观测缺表降级：LoadSymbols/Edges/… catch(CozoException)→空/null | OK（null 语义区分 external_call 缺表→BLOCKED 不装 PASS） | DepaScanPipeline.cs:485-488、:528-546 |
| 7 | 动作执行：事务内 runtime with{Store=tx} 下传，回调经 ctx 落同一事务 | OK | ConstraintLogic.cs:71-84、:137 |
| 8 | AddInterceptorAsync：先注册内存 registry 再写 db，db 失败则内存已注册（漂移） | RISK（低频；backlog B-1） | CozoOm.cs:179-189 |
