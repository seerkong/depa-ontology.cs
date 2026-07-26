# 现状报告 — cozo-lib-dotnet Om 层 DEPA 符合度（2026-07-06 快照）

## 总评

三 capsule 整体 DEPA 对齐**良好**：Om.Core 6/6（高）、Om.CodeKnowledge 5/6（高）、Om.Depa 3.5/6（混合偏高）。无结构性问题，无需立项重构；4 条真 GAP + 1 组误报的标注修正可收敛进一个小型 refactor track。

## 核心结论

1. **V-F2 五条全部误报**（inventory/incidents.md I-1）：OmMutationContext 不是 runtime 载体（真载体 CozoOmRuntime 是三字段纯数据 record），五个方法是零业务的单表达式委托、以 Runtime 为首参转发到 Logic 外部函数——恰是 DEPA 要求的形态。病灶在 heuristic 按名（含 "Context"）猜 carrier；补 depa-map.json 的 runtimeCarrierTypes 配置即消。
2. **真 GAP 四条**：om_entity 绕层直删+孤儿 om_property（I-2，最重）；scan 管线内文件 IO（I-3）；UtcNow 直取（I-4）；跨 capsule 引 internal 词表（I-5）。
3. **fact-grade 分级验证通过**：ck_* 观测层唯一写入者=Om.CodeKnowledge，depa_* 派生层唯一写入者=Om.Depa，scan 不回写观测行；唯一反写型红灯即 I-2。
4. **Effect 承诺裁定**：db 副作用意义上 CozoDbOmStore（经 ICozoOmStore）仍是唯一出口——成立；Om.Depa 的配置文件读取是承诺之外的第二类副作用，未走契约（I-3）。
5. **component-protocol 抽查 3 处全 PASS**，其中 `runtime with { Store = tx }` 事务重绑定（ConstraintLogic.cs:78-79）是 runtime 显式化的样板实现。

## 证据索引

- 问题清单：boundary/index.md；定级：boundary/fact-grades.md；唯一写入者：boundary/single-writer.md；反写：boundary/backwrite-risks.md。
- 逐条事故：inventory/incidents.md；归位表：inventory/fact-nodes.md；capsule 判定与打分：inventory/package-boundaries.md；读写路径：inventory/read-write-paths.md。
- 处置：convergence/decisions.md；切片：recommendations/。
