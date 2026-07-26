# convergence — 处置决策（环节④）

> 目录职责：holds=处置决策表；excludes=证据（inventory/）；tier=stable。

| # | 回溯 | 聚类维度 | 处置 | 去向 |
|---|------|----------|------|------|
| D-1 | I-1（V-F2×5） | 标注/工具 | 误报——代码不改；补 depa-map.json runtimeCarrierTypes 配置通道压制 heuristic；可选把便捷方法改扩展方法使 record 纯数据（零行为变化） | REC-4（P2） |
| D-2 | I-2 | Data/唯一写入者 | Om.Core 补 EntityLogic.DeleteEntityAsync（实体+属性+边一体清理，走 owner），Om.Depa 改调 | REC-1（P1） |
| D-3 | I-3 | Effect/分层 | 配置加载上提到公开入口边界（CozoOmDepaExtensions.DepaScanAsync 内先解析再进 pipeline），或加接受已解析 DepaMapConfig/effects 的 overload；pipeline 只吃数据 | REC-2（P2） |
| D-4 | I-4 | Runtime 显式化 | `DateTimeOffset.UtcNow` → `om.Runtime.Options.TimeProvider.GetUtcNow()` | REC-3（P2，一行） |
| D-5 | I-5 | 分层/internals | 二选一：EffectApiBuiltins 提升为 Om.CodeKnowledge 公开契约（连 EffectApiRule）；或将内建表移入 Om.Depa、CodeKnowledge 索引侧经参数注入。倾向前者（观测层拥有词表的 track 决策不动） | REC-5（P2） |
| D-6 | R-3、B-1 | 超范围/低频 | 登记 backlog 不展开 | recommendations/backlog.md |

三面速览：控制面（Registry/existential rules）、数据面（ck_* 观测 → depa_* 派生单向）、扩展面（depa-map/effects 配置通道 + heuristic 兜底）三面清晰，本轮无结构性重构必要——全部整改可在一个小型 refactor track 收敛。
