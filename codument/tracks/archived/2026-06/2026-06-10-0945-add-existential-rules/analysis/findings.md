# Findings

## Found Facts
- `cozo-lib-bun/cozo-om.js`（~5137 行）已具备本 track 需要复用的全部基础设施：
  - `createHash`（crypto）已在文件第 1 行引入，现用于 schema checksum（`_computeSchemaChecksum`），可直接用于 Skolem ID 生成
  - `om_edge` 为 bi-temporal 结构，「实体当前是否存在某关系边」的 `@NOW` 语义查询已有实现：`_getOutgoingNeighborsForPerm`（L1069，支持 asOf）、`getNeighborsAsOf`（L4361）
  - 多态类型闭包：`getDescendants`（L3301）/ `findByType`（L4575）已实现「父类型查询覆盖子类型实例」
  - 幂等写入：`upsertEntity`（L3489）+ 确定性 ID = chase 重跑天然幂等
  - alias 解析：`resolveType` / `resolveRel` / `resolveAttr`（L2872+）可让规则定义对 schema 重命名保持稳定（与 OM-022 权限策略同样的稳定性机制）
  - `initSchema`（L711）使用 `_runDslCreateIgnoreConflict` 模式幂等建表，新增 `om_existential_rule_def` 沿用该模式
  - schema snapshot 范围由 `_readSchemaSnapshotParts`（L1522）决定，新表需加入其中才能参与 snapshot/diff/rollback
- 现有 constraint 层（`defineConstraint` L197 / `validateConstraints` L311）是**逐实体 + JS 回调**形态：回调不可持久化、不可进 snapshot、集合扫描效率低，不适合承载存在规则
- `om_property` 同为 bi-temporal，`setProperty` / `linkEntities` 均支持 `validTime` 选项，chase 物化写入可直接带时间语义
- 测试命名惯例：`cozo-lib-bun/__tests__/om-<feature>.test.js`，门控命令为 `bun test`、`bun test --coverage`、`codument validate --strict`、viz 目录 `npm run test:e2e`
- viz 的 `/governance` 页（`cozo-lib-bun-viz/frontend/src/pages/GovernanceDemo.vue`）已有子 tab 结构（数据准备 / 权限查询），新增「完整性检查」tab 可复用骨架；server 端点集中在 `cozo-lib-bun-viz/server/src/index.js`，`/api/run` 已串行化避免 registry 竞态

## Constraints
- v1 head 形态仅支持 `exists: { rel, toType }`（∃ 关系边 + 目标实体）；属性存在性由 `required` + `validateConstraints` 覆盖，不重复建设（用户已确认）
- 不修改 CozoDB 内核（cozo-core）；全部实现在 cozo-om 应用层（Skolem 改写 + 宿主驱动循环）
- 不修改现有 constraint 层语义与 API；存在规则是平行子系统
- 旧 `/permission` demo 与 governance 页现有 tab 的行为不可改变
- 新表必须纳入 schema snapshot/rollback，否则破坏 OM-018~021 已交付的不变量
- chase 终止防护 v1 仅靠 maxIterations 兜底，不做静态无环预检（用户已确认）

## Open Questions
- （已全部决策，见 decisions.md）

## Conclusions
- 采用「Skolem 改写 + 外部驱动循环 + 违例检测查询」三件套，在 cozo-om 层实现 Nemo 存在规则的主要实际价值
- 新增声明式子系统：`om_existential_rule_def` 表 + `defineExistentialRule` / `listExistentialRules` / `checkExistentialRules` / `applyExistentialRules` 四个 API
- 违例检测为集合语义单查询（type 闭包 join property@NOW 过滤 + not-exists edge@NOW）；chase 在 JS 层循环至不动点或 maxIterations
- Skolem ID = `'skolem:' + sha256(ruleName + '|' + triggerEntityId).slice(0,16)`，确定性 + upsert 幂等；来源标记用内置属性 `_skolem_rule`（用户已确认）
- viz 演示放在 governance 页新增「完整性检查」子 tab（用户已确认）
