# Knowledge Context

## Source Notes
| Source | Summary | Relevance |
|--------|---------|-----------|
| `cozo-lib-bun/cozo-om.js` | 本体建模层全部实现：schema/继承/temporal/版本化/alias/权限/constraint | 本 track 的实现宿主，复用其查询片段与写入 API |
| `codument/specs/cozo-om/spec.md` | 已交付需求 OM-001~023 | 新需求顺延为 OM-024~027，且不得破坏既有不变量 |
| `codument/specs/cozo-lib-bun-viz/spec.md` | viz 已交付需求 VIZ-001~006 | 新增 VIZ-007（governance 完整性检查 tab） |
| cozo-docs `source/timetravel.rst` | Validity 类型 = [timestamp, assert/retract]，`@ ts` / `@ 'NOW'` 快照查询 | 违例检测的「边是否当前存在」必须按 NOW 语义判断 retract |
| Nemo / VLog（存在规则引擎） | skolem chase：头部存在变量用规则名+触发绑定的确定性函数生成新对象 | 本 track 在应用层复现 skolem chase；restricted chase 语义（先查满足再造对象）部分吸收 |

## Codebase Knowledge
- cozo-om 的 6 张核心表：`om_type` / `om_attr_def` / `om_rel_def`（TBox），`om_entity` / `om_property` / `om_edge`（ABox）；其中 property/edge 为 bi-temporal（valid_time: Validity + tx_time）
- 行为层注册表（action/mutation/interceptor/constraint/computed）是**内存 Map + 元数据表**双轨：JS 回调存内存，元数据表仅作存在性声明。存在规则与之不同——spec 是纯 JSON，可完整持久化
- schema 版本化机制：`applySchemaMigration` 升版本 + 写 snapshot；`rollbackSchema` 用 snapshot 恢复（strict 模式做实例数据兼容性预检）；snapshot 内容由 `_readSchemaSnapshotParts` 收集——**任何新 schema 元数据表都必须加入该函数**
- alias 机制：重命名通过 `om_*_alias` 映射表实现，读写 API 统一 canonicalize；引用名称的元数据（如权限策略）经 resolve 后对重命名免疫
- viz server 模式：demo 注册在 `server/src/demos/`，API 端点在 `server/src/index.js`；Playwright spec 用 `.pw.ts` 后缀避免被 bun test 误执行

## Domain Knowledge
- **存在规则（existential rule / TGD）**：∀x (body(x) → ∃y head(x,y))，头部含存在量词的 Datalog 扩展。Datalog 本身无法「发明」新对象
- **chase 算法**：反复应用规则直到所有存在约束满足。skolem chase 用确定性函数项造对象（可能造多余对象但实现简单）；restricted chase 先检查是否已有满足的对象（更省但判定更复杂）。两者对循环规则都可能不终止
- **本 track 的混合策略**：满足性检查在先（restricted 风格，已满足/人工补录不造对象）+ Skolem 确定性 ID（幂等可重跑）+ maxIterations（终止兜底）
- **主数据治理视角**：存在规则九成用于完整性约束（每个 X 必须有 Y），违例检测报告给管理员补录通常比自动造占位对象更符合业务语义——所以 `mode: 'check'` 是默认，`materialize` 是显式选择

## Terms
| Term | Meaning |
|------|---------|
| TGD | Tuple-Generating Dependency，存在规则的形式化名称 |
| Skolem 项 | 用确定性函数（此处为 hash）替代存在量词产生的对象标识 |
| chase | 反复应用 TGD 使数据满足所有存在约束的算法 |
| 不动点（fixpoint） | 一轮 chase 未产生任何新对象的收敛状态 |
| labeled null | chase 理论中「被发明」的占位对象；本实现中即 Skolem 实体 |
| 违例（violation） | 满足规则 body 但 head 不存在的实体 |
| forEach / exists | 规则 spec 中 body（触发范围）与 head（存在要求）的字段名 |
