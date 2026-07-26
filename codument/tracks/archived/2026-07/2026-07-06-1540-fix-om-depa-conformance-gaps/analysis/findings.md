# Findings

## Found Facts
- depa-expert 盘点全文在 analysis/depa-scan-workspace/（manifest 是状态真源）；V-F2 ×5 误报裁定证据链在其 report/。
- REC-1 根因：CozoOm 无删除实体 API；Om.Depa 清理段原生 :rm om_entity（DepaViolationDetectors.cs:726-732）；om_property 孤儿残留是 P2 时代已知妥协的根治。
- 全库唯一 UtcNow 直取：CozoOmCodeKnowledgeExtensions.cs:169。

## Constraints
- 删除语义=物理删（violation 是可重建投影；retract 留需求）；最小公开面（DeleteEntityAsync 是 delta 承诺的公开 API）；两侧测试全绿。

## Conclusions
- T1.1（REC-1）DONE 2026-07-06：
  - om_* 存储形态结论（SchemaLogic.InitSchemaAsync 是真源）：`om_entity {id => type_name, label}`；`om_property {entity_id, attr_name, valid_time: Validity => value, tx_time}`（时态多版本=同 entity_id/attr_name 多个 valid_time 键行）；`om_edge {from_id, rel_name, to_id, valid_time: Validity => props, tx_time}`——关系边是单表不按 relation 分表，from/to 各扫一遍即覆盖出入边。
  - 事务实现：`runtime.Store.BeginTransactionAsync(write: true)` + 三段脚本（rm om_property → rm om_edge 双规则 union → rm om_entity）+ `tx.CommitAsync`，与 ConstraintLogic.ExecuteActionAsync 同模式；`:rm` 对不存在键无害 ⇒ 幂等免查存在性。
  - Om.Depa 清理段（DepaViolationDetectors 原 720-732）整段改为一次 `om.DeleteEntityAsync(staleId, ct)`：原 UnlinkEntitiesAsync 循环（软 retract，留历史行）一并被物理级联替代，violates 边与 evidence_json 等属性行全清，P2 时代"温和残留"根治。
  - grep 证据：`:rm om_entity` 全 src 仅剩 EntityLogic.cs:65（API 本体）。
  - 测试：tests/Program.cs 新增 DeleteEntityAsync 级联/幂等段（delete-cascades 覆盖 2 个时态版本属性行 + 出入边 + 旁观实体不受扰；delete-idempotent 重删/删不存在 id）+ detect 段新增 depa-cleanup-no-orphans 断言（过期 violation 的 om_property 零行 + 全库 depa:violation 前缀无孤儿行）。两侧全绿：OM 套件 `dotnet run` passed；llm-wiki 套件 passed（eval-oracle must 26/26）。
- T2.1（REC-3/2/4/5）DONE 2026-07-06：
  - REC-3：CozoOmCodeKnowledgeExtensions.cs:169 `DateTimeOffset.UtcNow` → `om.Runtime.Options.TimeProvider.GetUtcNow()`（格式保持 "O"；TimeProvider 属性在 CozoOmOptions 上，与 EntityLogic.cs:102 用法一致）。grep 复核：src/ 全库已无 DateTimeOffset.UtcNow/DateTime.Now 直取（EffectApiBuiltins.cs:31 的 "System.DateTime.Now" 是白名单模式字符串，非直取）。
  - REC-2：DepaScanOptions add-only 新增 `Map`（DepaMapConfig?）/`Effects`（IReadOnlyList&lt;DepaEffectRule&gt;?）init 属性——非空优先于 MapPath/EffectsPath；入口层解析收口在 CozoOmDepaExtensions.ResolveScanInputs（internal，唯一路径→对象转换点）；DepaScanPipeline.ScanAsync 签名改为吃 (DepaMapConfig, IReadOnlyList&lt;DepaEffectRule&gt;?)；DepaOntologySchema.SyncEffectApisAsync 改吃已解析规则表；DepaReportQueries.GetConformanceReportAsync 亦经 ResolveScanInputs 解析后调管线。路径版行为完全兼容（薄壳）。grep 断言：DepaScanPipeline/DepaOntologySchema/DepaViolationDetectors 零 File.* 调用；File.* 仅剩 DepaConfigModels.Load 与 DepaEffectCatalog.LoadUserEffects（loader 本体），其调用点全部在 CozoOmDepaExtensions 入口层。
  - REC-4 放置结论：depa-map.json **两处都放**——仓根（proposal 承诺位；rootPath 按仓根相对 "cozo-lib-dotnet/src/Om.Core"）+ cozo-lib-dotnet/src/（P3 复扫可用位；rootPath "Om.Core"）。依据：RepositoryIndexer.cs:451 的 ck_file path 相对被索引根；LlmWikiToolRunner.ResolveDepaConfigPath 从 workDirectory 根找、.codument/ 兜底——历次 dogfood 的 workRoot=cozo-lib-dotnet/src，故 P3 复扫应以 src 下副本为准。两份均声明 runtimeCarrierTypes ["CozoOmRuntime","CozoOmOptions"] + Om.Core capsule（internalsGlob 默认）+ "_comment" 顶部字段说明用途（DepaMapConfig.Load 忽略未知键，安全）。
  - REC-5：零代码收口——decisions.md #2 已在（known-exception：Om.Depa 消费 Om.CodeKnowledge internal EffectApiBuiltins，观测层词表唯一合法消费者是解释层），核对无需再写。
  - 测试：tests/Program.cs 新增 config-at-entry（诱饵 MapPath + 不存在 EffectsPath + 注入 Map/Effects 对象 → scan 结果与文件版完全一致且诱饵 capsule 不物化、注入 effect 规则并入白名单）与 timeprovider-indexed-at（FixedTimeProvider 注入 → ck_meta.indexed_at 精确等于注入值）；测试先行确认编译失败仅因缺 Map/Effects API。两侧全绿：OM 套件 passed；llm-wiki 套件 passed（eval-oracle must 26/26）。
- T3.1（复扫与回归）DONE 2026-07-06：
  - 复扫探针 /tmp/depa-rescan-t31/（mem 库 + LlmWikiToolRunner 直调：index_repo(repoPath=cozo-lib-dotnet/src) → depa_conformance(workDirectory=同, scanFirst=true)；输出留 rescan-output.json）。
  - **首轮复扫暴露真 GAP**：V-F2 仍 GAP=5（OmMutationContext ×5，confidence 0.7=heuristic）——DepaScanPipeline 的 carrier heuristic 是"逐符号 fallback"，config 声明 runtimeCarrierTypes 只覆盖被声明类型本身，压制不了未声明的 "Context" 按名猜测，与 delta case carrier-false-positive-suppressed 的 SHALL 相悖（T2.1 该 case 有 delta 承诺但当时未落测试）。
  - 修复（最小、循已决决策 decisions.md #3 语义）：DepaScanPipeline.cs 载体 heuristic 改为**config 声明即封闭世界**——map.RuntimeCarrierTypes 非空时跳过按名（record 含 Runtime/Context）猜测；tests/Program.cs report 段 JobContext 双置信度场景改写为 suppression 断言（直接编码 carrier-false-positive-suppressed：JobContext 记录型+业务方法但不被猜为 carrier），V-F2 期望 2→1、persisted 同步、processor health GapCount 3→2。
  - **终轮复扫数字**（vs 整改前基线 analysis/depa-scan-workspace）：OmMutationContext 命中 **0**（基线 5）；V-F2 **PASS**（基线 GAP=5）；整体 GAP **0 条、无新增**；PASS=3（V-F2/V-L1/V-L3，基线 V-L1/V-L3 PASS 保持）；BLOCKED=8（V-D1/V-E1/V-E2/V-F1/V-S1 缺 config 输入 + V-D2/V-P1/V-A* 设计留白占位，与基线一致）；StructuralFindings=0；六维（data/effect/processor/layering/fact_source/actor）×11 规则行结构完整；index_repo：Communities=42、EntryPoints=230、Processes=85。
  - 两侧测试全绿：OM 套件 `dotnet run` passed；llm-wiki 套件 passed（eval-oracle must 26/26 = 100%）。
  - backlog 5 条（B-1 AddInterceptorAsync 内存先于 db 漂移 CozoOm.cs:187-197、B-2 depa: 前缀撞名防护 DepaScanPipeline.cs:68-96、B-3 工具侧 V-F2 纯委托豁免 DepaViolationDetectors.cs:504-546、B-4 scan 逐属性写无批量 DepaScanPipeline.cs:68-96、B-5 Support/ 混装 CozoDbOmStore.cs:7/CozoScriptBuilder.cs:3）已登记 codument/backlog/llm-wiki-p3-deferred.md 新小节"depa-expert 盘点 backlog（2026-07-06）"，每条带证据 path:line 与触发条件。
