# Track 实现 Gap 报告 — add-existential-rules（round 1）

- 协议：yield-gap-loop（validation_mode=yield-gap-loop, granularity=final_phase）
- scope：整个 track
- 日期：2026-06-10
- 历史报告：无（首轮）
- 结论：**NO_GAP**

## 1. 对照范围

目标文档：proposal.md、design.md、plan.xml、decisions.md、spec_deltas/cozo-om/delta.xml（OM-024~OM-027）、spec_deltas/cozo-lib-bun-viz/delta.xml（VIZ-007）。

被 review 的未提交实现：

- `cozo-lib-bun/cozo-om.js`（+466 行：om_existential_rule_def 建表、define/list/check/apply 四 API、snapshot/diff/rollback 集成、invalidateAliasCache）
- `cozo-lib-bun/cozo-om.d.ts`（+80 行类型定义）
- `cozo-lib-bun/__tests__/om-existential-{define,check,temporal,chase,fixpoint,versioning}.test.js`（新增，共 852 行）
- `cozo-lib-bun-viz/server/src/index.js`（+86 行：/api/governance/integrity/{seed-demo,rules,check,apply}，独立 integrityDb）
- `cozo-lib-bun-viz/server/src/governance-integrity-api.test.js`（新增）
- `cozo-lib-bun-viz/frontend/src/pages/GovernanceDemo.vue`（+226 行：「完整性检查」子 tab）
- `cozo-lib-bun-viz/e2e/governance-integrity.pw.ts`(新增)

## 2. Requirement / Case 逐条核对

### OM-024 规则定义与持久化（5/5 case 覆盖）

| case | 验证结论 | 证据 |
|---|---|---|
| define-and-persist | PASS | `om-existential-define.test.js` "defineExistentialRule persists and listExistentialRules returns it"；持久化字段（spec/mode/message/enabled/direction 默认 out）全部断言 |
| reject-invalid-spec | PASS | 同文件 "rejects spec without exists.rel or exists.toType" / "rejects unknown forEach.type / exists.toType / exists.rel" / "rejected define does not persist anything"（拒绝后不写入也有断言）；实现先 `_normalizeExistentialSpec` 校验再 put |
| redefine-upsert | PASS | "redefining the same rule name is an upsert"；实现用 `put` upsert 语义 |
| alias-canonicalized-on-define | PASS | "rule defined via alias names is persisted with canonical names"；实现经 `resolveType/resolveRel/_resolveAttrForCanonicalType` canonicalize 后持久化 |
| init-schema-idempotent | PASS | "initSchema is idempotent and preserves existing rule definitions"；建表走 `_runDslCreateIgnoreConflict` |

补充覆盖（超出 case 清单）：mode/where-op 非法值拒绝、enabled 持久化、空库 list 返回 []、初次 initSchema 即建表。

### OM-025 违例检测（6/6 case 覆盖)

| case | 验证结论 | 证据 |
|---|---|---|
| detect-missing-edge | PASS | `om-existential-check.test.js` 首个用例，断言完整 `{rule, entityId, message}` 形态 |
| satisfied-not-reported | PASS | 同用例中 o2（有边）不出现在结果中 |
| polymorphic-body-and-head | PASS | "polymorphic body and head matching"（RushOrder/ExpressShipment）；实现以 `getDescendants` 计算 body/head 类型闭包 |
| where-condition-filters-body | PASS | `om-existential-temporal.test.js` "only entities matching where enter the violation set"（含无属性实体不入 body）；另覆盖数值比较 op |
| retracted-edge-is-missing | PASS | "retracted edge counts as missing at NOW"（link→unlink 后 @NOW 报违例,unlink 前的 asOf 不报） |
| as-of-historical-check | PASS | "asOf reports historical violations before the edge existed"（T0 报、NOW 不报）；asOf 经 `_normalizeAsOfTimestamp` 校验,非法值拒绝有测试 |

补充覆盖：disabled 跳过、options.rules 过滤、direction:'in'、错误目标类型不满足 head、结果按 rule+entityId 排序。

### OM-026 Skolem chase（7/7 case 覆盖）

| case | 验证结论 | 证据 |
|---|---|---|
| materialize-creates-entity-and-edge | PASS | `om-existential-chase.test.js` 首个用例：created 记录、实体类型、labelTemplate、props、违例消失全断言 |
| skolem-marked-with-rule | PASS | "_skolem_rule" 用例；define 时为 toType 定义 `_skolem_rule` 属性,避免 validateEntity 报未定义属性 |
| idempotent-rerun | PASS | `om-existential-fixpoint.test.js` "rerun is idempotent"：created 为空、fixpoint true、全库仅一个 Shipment（确定性 sha256 截 16 hex ID,前缀 skolem:,与 design 一致） |
| manual-fix-respected | PASS | "manually fixed entities are not materialized"（满足性先检：物化前重查违例集） |
| iteration-cap-with-diagnostics | PASS | "cyclic rules stop at maxIterations with diagnostics"（A↔B 循环规则,maxIterations:3,reachedFixpoint:false,diagnostics 含 ruleName/remainingViolations） |
| valid-time-applied | PASS | "validTime is applied..."：getNeighborsAsOf 在 T 前查不到、T 后查到 |
| check-mode-rules-not-materialized | PASS | "check-mode rules are not materialized"（check 违例保持可检出） |

补充覆盖：direction:'in' 物化方向、options.rules 限定、非法 maxIterations 拒绝。

### OM-027 治理集成（4/4 case 覆盖）

| case | 验证结论 | 证据 |
|---|---|---|
| snapshot-includes-rules | PASS | `om-existential-versioning.test.js` "snapshot includes existential rule definitions"；`_readSchemaSnapshotParts`/`_composeSchemaSnapshot` 已纳入 om_existential_rule_def；diff 用例（applySchemaMigration 后 diffSchemaVersions 报 added/updated）一并验证 |
| rollback-restores-rules | PASS | "rollback restores the historical rule definition"（v2 改 spec 后回滚 v1 恢复）；另有 legacy snapshot 无规则 section 时回滚恢复为空集不报错的用例（design 兼容性条款） |
| alias-keeps-rule-working-after-rename | PASS | "rule keeps working after the relation is renamed via alias"（旧名边、新名边均满足,缺边者报违例）；实现运行时 `_resolveExistentialRuleRuntime` 重解析并将 alias 名并入匹配集；新增导出 `invalidateAliasCache` 处理长生命 runner 的带外 alias 写入 |
| backward-compatible-when-unused | PASS | "no rules defined: check and apply are no-ops" + 全量既有回归（om-backward-compat / om-exports 等 48 文件全绿,见 §3）；未定义规则时无新查询路径触发 |

### VIZ-007 完整性检查子 tab（3/3 case 覆盖)

| case | 验证结论 | 证据 |
|---|---|---|
| tab-shows-violations | PASS | E2E "integrity: seed-demo -> check shows violations -> apply clears them"（规则表含 asset_must_have_owner,违例表含两个孤儿资产);server 单测 happy path 同步覆盖 |
| chase-materializes-and-clears | PASS | 同 E2E：integrity-created 含 skolem:、re-check 后 integrity-no-violations 可见;server 单测断言 created.length=2、reachedFixpoint、recheck 为空 |
| existing-tabs-unaffected | PASS | E2E "integrity tab does not affect the existing governance query flow"（先动 integrity 再走 prep+query 全流程）+ 既有 governance-schema.pw.ts 与 smoke.pw.ts permission 用例全绿;server 单测验证 integrityDb 与 /api/governance/seed 隔离 |

server 端三个 spec 要求端点（rules/check/apply）齐备,另加 seed-demo 重置端点（演示可确定性重置,server 单测覆盖,属增量便利,非偏差）。

## 3. 门控命令运行结果（全部通过)

| 命令 | 位置 | 结果 |
|---|---|---|
| `bun test` | 仓库根 | 239 pass / 0 fail（48 文件,含全部 om-existential-* 与 governance-integrity-api 单测） |
| `bun test --coverage` | cozo-lib-bun/ | 231 pass / 0 fail;cozo-om.js 行覆盖 91.38%;新增 existential 段（约 5130-5540 行）未覆盖仅 8 行防御性 throw/rethrow 分支（5169、5174、5214、5218、5239、5247、5513-5514）,新代码行覆盖 ≈98%（>80% 达标） |
| `codument validate --strict` | 仓库根 | 3 passed / 0 failed（track/add-existential-rules、spec/cozo-om、spec/cozo-lib-bun-viz） |
| `CI=true npm run test:e2e` | cozo-lib-bun-viz/ | 14 passed（含 2 个新 governance-integrity 用例与全部既有回归） |

## 4. 已确认设计取舍（不算 gap）

- v1 head 仅 `exists:{rel,direction?,toType}`（决策 1）
- 终止防护仅 maxIterations 兜底（决策 5）
- `_skolem_rule` 用内置属性标记（决策 4;实现在 materialize 规则 define 时为 toType 注册该属性定义,属决策的合理落地）
- viz 用 governance 子 tab（决策 3）
- 前端在页面内用 postJson/fetch 调用而未改 api.ts（与该页面既有惯例一致,用户已接受）
- integrity demo 使用独立 integrityDb（隔离并行测试,用户已接受）

## 5. Gap 列表

**无 gap。**

- spec_deltas 共 25 个 case（OM-024×5、OM-025×6、OM-026×7、OM-027×4、VIZ-007×3）全部有对应实现与测试覆盖,且四项门控全绿。
- plan.xml 全部 task/subtask 为 DONE、acceptance criteria 均 checked,与实际实现一致,无需修改。
- design.md、spec_deltas 与实现一致（design.md 提及 api.ts 的差异属已接受取舍）,无需修改。

## 6. 本轮动作

- 未做任何实现/文档修改（无必要)
- 返回状态：NO_GAP
