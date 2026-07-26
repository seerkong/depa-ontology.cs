# recommendations — 整改清单（环节⑤）

> 目录职责：holds=可独立落地的建议切片 + backlog；tier=stable；建议不等于改造。

## 索引（一个 refactor track 内的建议顺序：REC-1 → REC-3 → REC-2 → REC-4 → REC-5）

| ID | 维度 | 裁定 | 优先级 | 一句话 | 破坏公开 API？ |
|----|------|------|--------|--------|----------------|
| REC-1 | Data/唯一写入者 | GAP | P0 | Om.Core 补 DeleteEntityAsync（实体+属性+边一体删），Om.Depa 过期清理改调，消孤儿 om_property | 否（新增 API）|
| REC-2 | Effect/分层 | GAP | P1 | depa 配置解析上提到公开入口边界，pipeline 只吃已解析数据 | 否（内部重排/加 overload）|
| REC-3 | Runtime 显式化 | GAP | P2 | ck_meta.indexed_at 改走 TimeProvider | 否（一行）|
| REC-4 | 标注/工具 | 误报 | P1（标注侧） | depa-map.json 声明 runtimeCarrierTypes 压制 V-F2 heuristic；可选把 OmMutationContext 便捷方法改扩展方法 | 标注零改；可选项改 API 形态（源兼容）|
| REC-5 | 分层/internals | GAP | P2 | EffectApiBuiltins/EffectApiRule 提升为 Om.CodeKnowledge 公开契约 | 扩大公开面（需评审）|

详情各见 REC-*.md；超范围登记 backlog.md。
