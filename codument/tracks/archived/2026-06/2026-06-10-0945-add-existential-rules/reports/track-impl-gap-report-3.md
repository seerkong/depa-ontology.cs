# Track 实现 Gap 报告 — add-existential-rules（round 3，FIX_APPLIED 后复检轮）

- 协议：yield-gap-loop（validation_mode=yield-gap-loop, granularity=final_phase）
- scope：整个 track
- 日期：2026-06-10
- 历史报告：reports/track-impl-gap-report-1.md（round 1，NO_GAP）、reports/track-impl-gap-report-2.md（round 2，FIX_APPLIED）
- 本轮性质：对 round 2 两项修复的独立复检（正确性 / 完整性 / 无副作用）+ 整体无新 gap 确认
- 结论：**NO_GAP**（round 2 修复确认正确且无副作用，四项门控全绿，本轮未做任何修改）

## 1. Round 2 修复点复检

### 1.1 修复 1：attemptKey 分隔符（cozo-om.js）— 确认正确

- `applyExistentialRules` 中去重键现为显式转义 `` `${rule.ruleName}${v.entityId}` ``（cozo-lib-bun/cozo-om.js:5451），紧邻注释明确说明用途（防 ('r1'+'2x') 与 ('r12'+'x') 拼接歧义，5449-5450 行）。
- 全文件字节级扫描（`LC_ALL=C grep -c $'\x01'`）确认 cozo-om.js 与 __tests__/ 下**不再存在任何裸 0x01 控制字节**（计数为 0）；文件内其余 5 处 `` 复合键（约束比较去重、属性 memo 键、关系组键、snapshot keyString）均为既有显式转义写法，风格一致。
- 语义复核：`attempted` 集合在整个 apply 调用内跨迭代持久，若键碰撞，后处理的 (rule, entity) 违例将被**永久跳过**（而非延迟到下一轮），导致少物化 + reachedFixpoint=false。分隔符消除该风险，且不改变任何对外行为。

### 1.2 修复 1 的回归测试 — 确认真实验证目标行为

`om-existential-fixpoint.test.js` "colliding rule/entity name concatenations do not shadow each other"（94-123 行）：

- 构造真实碰撞对：规则 `r1` × 实体 `2x` 与规则 `r12` × 实体 `x`，无分隔拼接均为 `r12x`；两条规则作用于不相交的类型/关系（TypeA/rel_a/TargetA 与 TypeB/rel_b/TargetB），排除其它满足路径干扰。
- 断言链完整：单次 apply 内两个 (rule|triggerEntityId) 对**均**出现在 created、`reachedFixpoint === true`、re-check 违例为空。若退化为无分隔拼接，第二个违例被 attempted 跳过 → created 缺一项 + fixpoint=false + re-check 非空，三重断言均会失败。测试有效固化分隔符语义。

### 1.3 修复 2：裸库 no-op 边界测试 — 确认真实验证目标行为

`om-existential-fixpoint.test.js` "check and apply on a db without initSchema are safe no-ops"(125-131 行)：

- 直接 `new CozoDb('mem', '', {})` 不经 createTestDb/initSchema，确实走裸库路径。
- 实现路径复核：check/apply 均经 `listExistentialRules` → fromStored('om_existential_rule_def') 失败 → `_isStoredRelationMissingError` 捕获返回 []（cozo-om.js:5515-5518）→ check 返回 []、apply 返回 `{created:[], reachedFixpoint:true}`，与测试断言逐一对应。无抛错即验证了"安全 no-op"。

### 1.4 无副作用确认

- 修复仅触及 cozo-om.js 单行键表示（字节级等价改写 + 注释）与测试文件增量；d.ts、spec_deltas、design.md、plan.xml 均无需且未发生语义改动。
- 全部既有测试仍绿（见 §3）：根 241/241（239 既有 + 2 新增）、cozo-lib-bun 233/233、e2e 14/14。

## 2. 横向抽查（修复点附近 requirement 对应）

- OM-025 `checkExistentialRules`（cozo-om.js:5370-5394）：enabled 过滤、options.rules 过滤、asOf 规范化校验、rule+entityId 排序，与 spec 语句逐项对应；spec_deltas/cozo-om/delta.xml 6 case 在 om-existential-check / om-existential-temporal 测试中均有实质对应（round 1/2 已全量核对，本轮抽查一致）。
- OM-026 `applyExistentialRules`（5413-5500）：materialize-only 过滤、满足性先检（每轮重查违例集）、确定性 Skolem ID（sha256 截 16 hex，`skolem:` 前缀）、`_skolem_rule` 写入、direction:'in' 物化方向、validTime 透传（writeOpts）、maxIterations 兜底 + diagnostics（ruleName/remainingViolations 排序输出），均与 spec/design 一致。
- 未发现新 gap。

## 3. 四项门控重跑结果（本轮，全部通过）

| 命令 | 位置 | 结果 |
|---|---|---|
| `bun test` | 仓库根 | 241 pass / 0 fail（48 文件） |
| `bun test --coverage` | cozo-lib-bun/ | 233 pass / 0 fail；cozo-om.js 行覆盖 91.45%；existential 段未覆盖仍仅 7 行防御性 throw/rethrow（5169/5174/5214/5218/5239/5247/5517），与 round 2 完全一致，>80% 达标 |
| `codument validate --strict` | 仓库根 | 3 passed / 0 failed（track/add-existential-rules、spec/cozo-om、spec/cozo-lib-bun-viz） |
| `CI=true npm run test:e2e` | cozo-lib-bun-viz/ | 首跑 13 passed + 1 flaky（smoke.pw.ts:363 既有 permission-demo 滚动用例，重试通过，与本 track 无关，round 2 已记录同源 flaky）；干净复跑 **14 passed，exit 0** |

## 4. 已确认设计取舍（复核，不算 gap）

v1 head 仅 exists:{rel,toType}；仅 maxIterations 兜底；`_skolem_rule` 内置属性；governance 子 tab；页面内 postJson；独立 integrityDb —— 与 decisions.md 一致，维持既往认定。

## 5. Gap 列表与本轮动作

**无 gap。**

- round 2 两项修复经独立复检均正确、完整、无副作用；碰撞测试与裸库测试均真实验证目标行为而非表面断言。
- 本轮未做任何实现/文档修改；plan.xml / spec_deltas / design.md 均无需变更。
- 返回状态：NO_GAP
