# Track 实现 Gap 报告 — add-existential-rules（round 2，验证轮）

- 协议：yield-gap-loop（validation_mode=yield-gap-loop, granularity=final_phase）
- scope：整个 track
- 日期：2026-06-10
- 历史报告：reports/track-impl-gap-report-1.md（round 1，NO_GAP）
- 本轮性质：对 round 1 NO_GAP 的独立怀疑性验证轮，不信任 round 1 结论，全部独立重核
- 结论：**FIX_APPLIED**（功能层面证实无 spec/design 偏差；发现并修复 1 项工程卫生缺陷，补 2 个边界回归测试）

## 1. 独立重核范围与方法

重新通读 proposal.md、design.md、plan.xml、decisions.md、两份 spec_deltas（OM-024~027 共 22 case、VIZ-007 共 3 case），并逐行 review 全部未提交 diff：

- `cozo-lib-bun/cozo-om.js`（existential section 全量 + initSchema/snapshot/diff/rollback 接入点）
- `cozo-lib-bun/cozo-om.d.ts`
- `cozo-lib-bun/__tests__/om-existential-{define,check,temporal,chase,fixpoint,versioning}.test.js`（逐用例核对 GIVEN/WHEN/THEN 实质对应）
- `cozo-lib-bun-viz/server/src/index.js` + `governance-integrity-api.test.js`
- `cozo-lib-bun-viz/frontend/src/pages/GovernanceDemo.vue` + `e2e/governance-integrity.pw.ts`

## 2. Round 1 之外的怀疑角度逐项核验

### 2.1 回归视角（既有功能未被破坏）— PASS

- `GovernanceDemo.vue` diff 为纯增量：既有 query/prep 两个 tab 的模板、状态与函数一行未动；新增内容全部 `v-if="activeTab === 'integrity'"` 隔离；样式新增 `.integrity-table` 不影响既有选择器。
- `server/src/index.js` 新端点独立挂载，integrityDb 与 governanceDb/sharedDb 隔离；`close()` 增补 integrityDb 关闭，无泄漏。
- om-backward-compat / om-perm-check-access / om-perm-schema / om-schema-{migration-apply,rollback,snapshot-diff,versioning-metadata} 等既有回归全部在门控中全绿（见 §4）。
- E2E 既有 12 个用例（smoke / governance-schema）全部通过；server 单测验证 `/api/governance/seed` 重置不影响 integrity 状态。

### 2.2 边界与负面用例 — PASS（本轮补 1 个测试）

- defineExistentialRule 非法输入：缺 forEach.type / exists.rel / exists.toType、未知 type/rel、非法 mode、非法 where op 均有拒绝测试，且「拒绝后不写入」有断言。
- `checkExistentialRules` / `applyExistentialRules` 在**未 initSchema** 的裸库上：实现经 `listExistentialRules` 的 `_isStoredRelationMissingError` 捕获返回 []/no-op；round 1 仅有「fresh schema（已 initSchema）返回 []」用例，未覆盖裸库路径 → 本轮补测试 "check and apply on a db without initSchema are safe no-ops"（om-existential-fixpoint.test.js），通过。
- `applyExistentialRules` 对 `direction:'in'` 的物化端点方向：实现为 `linkEntities(skolemId, rel, v.entityId)`（Skolem 在 from 侧），与检测查询 `from_id: other_id, to_id: id` 的 sat 原子方向一致；已有测试以 `getNeighbors(d1, 'belongs_to', 'incoming')` 断言。方向正确。
- asOf 同时作用于 where 属性条件与边存在性（temporal 测试第 3 例），非法 asOf 拒绝有测试。

### 2.3 一致性 — PASS

- d.ts 与 js 签名逐一对照：define/list/check/apply 四 API 的参数、options 形态（rules/asOf；rules/maxIterations/validTime）、返回结构（ExistentialViolation、ExistentialChaseResult 含 diagnostics）与实现一致；`invalidateAliasCache` 已声明。
- spec_deltas 25 个 case 与测试断言实质对应（非表面相似），抽查重点：retracted-edge 用例同时断言 unlink 前 asOf 不报；iteration-cap 用例真实构造 A↔B 循环规则并断言 iterations=3 / reachedFixpoint=false / diagnostics 结构；alias-rename 用例同时覆盖旧名边、新名边、缺边三类实体。
- 一处 d.ts 容差：listExistentialRules 在 spec JSON 损坏时防御性返回 `spec: null`，d.ts 未标注 null —— 纯防御分支（无写入路径可产生坏 JSON），不算 gap。

### 2.4 工程卫生 — 发现 1 项缺陷，已修复

**发现：** `applyExistentialRules` 的 chase 去重键源代码中嵌入了**裸 \x01 控制字节**（`` `${rule.ruleName}<0x01>${v.entityId}` ``）。该字节在编辑器/Read/grep/diff 中均不可见，review 时看上去是无分隔符的裸拼接（会让 ('r1'+'2x') 与 ('r12'+'x') 碰撞）。本轮先按"无分隔符"假设写了碰撞回归测试，行为验证证明分隔符**功能上存在且正确**（两个违例均被物化、reachedFixpoint=true），但不可见控制字节本身是真实的可维护性缺陷：审阅者无法看见它，极易在未来重构中被无意删除而引入真碰撞 bug。

**修复（不涉及任何行为变化，spec/design/plan 均无需改动）：**
1. `cozo-lib-bun/cozo-om.js`：将裸 \x01 字节改写为显式转义 `\u0001` 并加注释说明其作用（防 (rule, entity) 拼接歧义）；全仓扫描确认不再有裸控制字节。
2. `cozo-lib-bun/__tests__/om-existential-fixpoint.test.js`：新增碰撞回归测试 "colliding rule/entity name concatenations do not shadow each other"（固化分隔符语义，防未来回归）与裸库边界测试（见 §2.2）。

- 其余卫生项：新代码无 console.log/debugger/TODO/FIXME；无临时文件（本轮调试脚本已删除）；plan.xml 全部 task/subtask DONE 且 AC checked，与实现一致；P5 `<confirm status="IN_PROGRESS">` 与 gap_loop_round=2 反映 loop 进行中，正确。

## 3. 已确认设计取舍（复核，不算 gap）

v1 head 仅 exists:{rel,toType}；仅 maxIterations 兜底；`_skolem_rule` 内置属性（define materialize 规则时注册属性定义属合理落地）；governance 子 tab；页面内 postJson 而非 api.ts；独立 integrityDb —— 均与 decisions.md 确认记录一致。

## 4. 门控命令重跑结果（修复后，全部通过）

| 命令 | 位置 | 结果 |
|---|---|---|
| `bun test` | 仓库根 | 241 pass / 0 fail（48 文件；239 + 本轮新增 2） |
| `bun test --coverage` | cozo-lib-bun/ | 233 pass / 0 fail；cozo-om.js 行覆盖 91.45%；existential 段未覆盖仅剩 7 行防御性 throw/rethrow（5169/5174/5214/5218/5239/5247/5517），新代码覆盖 >80% 达标 |
| `codument validate --strict` | 仓库根 | 3 passed / 0 failed |
| `CI=true npm run test:e2e` | cozo-lib-bun-viz/ | 14 passed，exit 0（其间一次运行中 smoke.pw.ts 既有 RBAC 用例出现一次 flaky 重试后通过，与本 track 改动无关；复跑 14/14 干净通过） |

## 5. Gap 列表与本轮动作

| # | 类别 | 内容 | 处置 |
|---|---|---|---|
| 1 | 工程卫生（minor） | chase 去重键中嵌入不可见裸 \x01 控制字节，review 不可见、易被误删引入碰撞 bug | 已修复为显式 `\u0001` 转义 + 注释 + 碰撞回归测试 |
| 2 | 测试覆盖（minor） | 未 initSchema 裸库上 check/apply 的边界行为无测试 | 已补测试，通过 |

- 功能层面：25 个 spec case 全部确认有实质对应的实现与测试，round 1 的功能性结论经独立重核成立；无 spec/design/plan 偏差。
- 文档：spec_deltas / design.md / plan.xml 均无需修改（修复为实现内部表示与测试增量，不改变任何对外语义）。
- 返回状态：FIX_APPLIED
