# Track 实现 Gap 报告 — add-existential-rules(round 5,round 4 FIX_APPLIED 后的独立复检轮)

- 协议:yield-gap-loop(validation_mode=yield-gap-loop, granularity=final_phase)
- scope:整个 track(复检重点为 round 4 的 2 项修复)
- 日期:2026-06-10
- 历史报告:round 1(NO_GAP)、round 2(FIX_APPLIED:attemptKey 分隔符 + 碰撞/裸库测试)、round 3(NO_GAP 复检收口)、round 4(用户显式触发,FIX_APPLIED:where value 校验 + design.md 违例查询执行模型修订)
- 本轮性质:对 round 4 两项修复做独立复检——正确性、完整性、无副作用,并确认无新 gap
- 结论:**NO_GAP**(两项修复均正确完整、文档与实现一致、无副作用;四项门控全绿;本轮零修改)

## 1. 复检项 1:where value 校验(round 4 修复 #1)— PASS

### 1.1 实现核对(cozo-lib-bun/cozo-om.js)

- `_normalizeExistentialSpec` 中校验位于 cozo-om.js:5229-5231:`!Object.prototype.hasOwnProperty.call(cond, 'value') || cond.value === undefined` ——**同时覆盖「缺 key」与「显式 undefined」两种形态**(hasOwnProperty 捕获缺键,`=== undefined` 捕获显式 undefined;两者必须分别判定,因为 `{value: undefined}` 有键但取值 undefined)。
- 错误信息 `forEach.where entries require an explicit value (attr 'x'); use null for a null comparison` ——含出错条目的 attr 名(可定位)与修正指引(null 替代),可诊断性达标。
- 行内注释(5226-5228)如实记录根因:undefined 会被 JSON 持久化丢键,落库 spec 静默永不匹配。
- **null 不被误拒**:校验仅排除 undefined,`value: null` 通过并经 5233 行 `where.push({ attr, op, value: cond.value })` 原样持久化。
- 校验时序合理:在 attr 必填、op 白名单之后、`_resolveAttrForCanonicalType` 之前——拒绝先于任何 DB 查询副作用;且整个 normalize 在 `put('om_existential_rule_def', ...)` 之前完成,**拒绝路径不落库**。
- 类型声明一致:cozo-om.d.ts:477-481 `ExistentialWhereCondition` 中 `value: unknown` 为必填(非 optional),与运行时校验语义吻合,无需追加修改。

### 1.2 回归测试核对(__tests__/om-existential-define.test.js:157-182)

测试 "rejects where entry without an explicit value (silent dead-rule guard)" 为**真实验证**,四段断言齐备:
1. 缺键形态 `{ attr, op: '=' }` → rejects `/require an explicit value/`;
2. 显式 undefined 形态 `{ attr, op: '=', value: undefined }` → rejects 同一错误;
3. 双拒绝后 `listExistentialRules(db)` 为空 → 验证拒绝不落库;
4. `value: null` 定义成功且 `rules[0].spec.forEach.where` 精确等于 `[{ attr: 'status', op: '=', value: null }]` → 验证 null 合法且 JSON 往返保真。

### 1.3 spec delta 措辞核对(spec_deltas/cozo-om/delta.xml)

OM-024 `reject-invalid-spec` case 的 when 为「…或 where 条目缺少 value(undefined)」,then 为「系统返回错误且不写入规则」——与实现行为(缺键/undefined 双拒绝 + 不落库)及测试断言一致。`codument validate --strict` 通过,delta XML 结构合法。

## 2. 复检项 2:design.md 违例查询执行模型修订(round 4 修复 #2)— PASS

逐条与实现 `_findExistentialViolations`(cozo-om.js:5334-5374)+ `_resolveExistentialRuleRuntime`(5300-5330)对照:

| design.md 声明 | 实现事实 | 一致? |
|---|---|---|
| §3(行 24):「每条规则编译为一个集合语义查询(body 实例集 − head 满足集,`not sat[id]` 在引擎内求差),按 body 类型闭包逐类型名执行一次」 | 单 script:`sat[id] := ...` + `?[id] := *om_entity{...}, not sat[id]`;5367 行 `for (const typeName of rt.bodyTypeNames)` 逐类型名各执行一次 | ✓ |
| §3:「head 的 toType 闭包与 rel 别名集以参数传入、由 `is_in` 在单查询内判定」 | 5338 行 `params = { rel_names, to_types }`;script 中 `is_in(rn, $rel_names)`、`is_in(other_type, $to_types)` | ✓ |
| §3:「与 findByType/aggregateByType 的既有逐类型模式一致」 | bodyTypeNames = canonical + getDescendants 闭包 + 各自 alias,排序后逐个查询,与既有模式同构 | ✓ |
| 风险节(行 76):「type 闭包在查询前用 JS 计算…toType 闭包/rel 别名集以参数传入单查询,body 闭包逐类型名执行——查询次数与类型闭包大小(schema 规模)成正比,与实例数无关」 | `_resolveExistentialRuleRuntime` 在 JS 侧计算全部闭包;查询次数 = |bodyTypeNames|,与实例数无关 | ✓ |
| §2(行 17):「where 条目必须显式给出 value(undefined 在 define 时拒绝——JSON 持久化会丢弃 undefined 键…;null 合法)」 | 即复检项 1 的实现语义 | ✓ |

design.md 修订后与实现**完全一致**,无残留漂移;round 4 选择「改文档不改实现」的理由(逐类型为仓库既定惯例、行为正确、改单查询属无必要行为面变更)复核后维持成立。

## 3. 副作用复检 — PASS

- 行为变更面仅为「原静默接受坏 spec → define 时拒绝」:根目录全部 242 测试(241 既有 + round 4 新增 1)零修改通过,无既有合法用法受影响。
- 校验逻辑不触达 check/apply/temporal/versioning 路径(仅 define 入口),existential 六个测试文件 + server 单测 + e2e 全部通过印证。
- 覆盖率中 existential 段未覆盖行仍为同一组 7 行防御性 throw(5169/5174/5214/5218/5245/5253/5523),与 round 4 报告记录逐行一致;新增校验行(5229-5230)已被测试覆盖。
- plan.xml 无需变更复核成立:修复落在 T1.2(status=DONE)的「where 合法性」校验范围内(plan.xml:39-44),无新增任务或 AC 变化。

## 4. 新 gap 扫描 — 无

- 本轮未发现 round 1-4 之外的新 gap;round 4 报告 §4 两项观察(materialize.props define 时不做键校验、违例查询可整库统一压缩为 is_in 单查询)维持为非 gap 的后续增强候选。
- 已确认设计取舍(v1 head 仅 exists:{rel,toType}、仅 maxIterations 兜底、_skolem_rule 内置属性、governance 子 tab、页面内 postJson、独立 integrityDb)与 decisions.md 一致,维持既往认定,不算 gap。

## 5. 门控命令运行结果(本轮,全部通过)

| 命令 | 位置 | 结果 |
|---|---|---|
| `bun test` | 仓库根 | 242 pass / 0 fail(48 文件,647 expect) |
| `bun test --coverage` | cozo-lib-bun/ | 234 pass / 0 fail;cozo-om.js 行覆盖 91.46%(>80% 达标);All files 92.57% |
| `codument validate --strict` | 仓库根 | 3 passed / 0 failed(track/add-existential-rules、spec/cozo-om、spec/cozo-lib-bun-viz) |
| `CI=true npm run test:e2e` | cozo-lib-bun-viz/ | 两次运行均 13 passed + 1 flaky、exit 0(首跑 flaky 为 smoke.pw.ts:133 permission demo,复跑 flaky 为 smoke.pw.ts:98 permission demo——同属 round 2/3/4 已记录的既有 permission demo flaky 族,重试均通过,与本 track 无关;governance-integrity.pw.ts 两次均一次通过) |

## 6. 本轮动作与返回

- 复检 round 4 两项修复:实现、测试、spec delta、design.md 四面核对均正确完整、无副作用;无新 gap。
- 本轮**零修改**(plan/spec/design 均未变更)。
- 返回状态:NO_GAP。
