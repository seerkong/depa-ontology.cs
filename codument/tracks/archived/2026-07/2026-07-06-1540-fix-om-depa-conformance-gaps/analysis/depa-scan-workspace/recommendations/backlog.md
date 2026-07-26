# backlog — 超范围发现（登记不展开）

| # | 发现 | 证据 | 建议去向 |
|---|------|------|----------|
| B-1 | CozoOm.AddInterceptorAsync 先改内存 Registry 再写 db，db 失败时内存已注册（内存/db 漂移，非幂等）；DefineActionAsync/DefineMutationAsync 反序（db 先），失败后 db 有定义无 handler——执行时显式报错，可接受但两处顺序纪律不一致 | CozoOm.cs:157-189 | 未来 Om.Core 一致性小切片：统一"db 成功后才登记内存"顺序 |
| B-2 | depa 派生实体与用户业务实体同居 om_entity（`depa:` 前缀软隔离），用户实体撞前缀无防护 | DepaScanPipeline.cs:66 | 可在 InitDepaOntologyAsync 文档声明保留前缀，或存量校验 |
| B-3 | depa_conformance 工具 V-F2 检测器可加"纯委托豁免"（方法体仅一条 CALLS 且目标首参为 runtime 载体类型 → 降 INFO） | DepaViolationDetectors.cs:503-544 | 工具自身 track（理论同源项目） |
| B-4 | DepaScanPipeline 每实体多次 SetPropertyAsync 逐条 RunAsync（无事务批量），大仓扫描往返放大 | DepaScanPipeline.cs:66-96 | 性能切片：scan 段套事务/批量 put |
| B-5 | Om.Core Support/ 混装 effect 实现（CozoDbOmStore）与纯 helper（CozoScriptBuilder），目录语义可再分（Effects/ vs Support/） | CozoDbOmStore.cs:7、CozoScriptBuilder.cs:3 | 美化级，随下次 Om.Core 结构调整顺带 |
