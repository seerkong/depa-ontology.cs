# Track 实现 Gap 报告 — add-existential-rules（round 4，用户显式触发的全新视角轮）

- 协议：yield-gap-loop（validation_mode=yield-gap-loop, granularity=final_phase）
- scope：整个 track
- 日期：2026-06-10
- 历史报告：round 1（NO_GAP）、round 2（FIX_APPLIED：attemptKey 分隔符 + 碰撞/裸库测试）、round 3（NO_GAP 复检收口）
- 本轮性质：track 已 completed、P5 confirm DONE 后用户再次触发；不预设已收口，以前三轮未覆盖/覆盖较浅的角度（并发状态、数据形态、性能规模、文档一致性、spec↔测试双向核对）做全新审查
- 结论：**FIX_APPLIED**（功能层面 25 个 spec case 维持全覆盖；发现并修复 1 项实现级 minor gap、1 项 design.md 文档漂移；四项门控全绿）

## 1. 本轮新审查角度与结论

### 1.1 并发 / 状态角度 — PASS

- **alias 缓存与多 db 实例**：模块级缓存为 `_aliasCacheByRunner = new WeakMap()`（cozo-om.js:2687），严格按 runner 实例隔离，多 db 并存无串扰；`applySchemaMigration`/`rollbackSchema` 路径上有三处显式 `delete`（2513/2614/2678），带外写 alias 用 `invalidateAliasCache` 兜底。无全局可变状态泄漏。
- **integrity 端点与其他端点交错**：server 全部端点（含新增 integrity 4 个）统一经 `withOmRegistryLock` 串行队列（index.js:20），交错调用天然串行化。`seed-demo` 内的 `om.clearRegistry()` 与既有 `/api/governance/seed`（601-602 行）模式一致；存在规则子系统不依赖内存 constraint registry（spec 持久化在 db、apply 用 skipConstraints），clearRegistry 交错对其无影响。`seed-demo` 在锁内构建新 db、swap 后 close 旧 `integrityDb`，失败路径 close 新 db；在锁内执行故无 use-after-close 窗口。server 单测验证 `/api/governance/seed` 重置不影响 integrity 状态。

### 1.2 数据形态角度 — 发现 1 项 minor gap（已修复），其余 PASS

- **规则名/实体 ID 含引号、反斜杠、unicode**：所有查询经 param 传值（无字符串拼接进 CozoScript）；实测规则名 `规则"带'引号"` × 实体 `o"嗨'\1` 全链路（define→check→apply→re-check）正确，skolemId 哈希域亦无注入面。PASS。
- **where.value 为 null**：可表示的 JSON 值，比较语义一致（与字符串属性比较不匹配，规则不误报）。PASS，本轮补测试固化「null 合法」。
- **where 条目缺少 value / value 为 undefined（GAP，已修复）**：`_normalizeExistentialSpec` 原先接受缺 value 的 where 条目；`JSON.stringify` 持久化时静默丢弃 undefined 键，落库 spec 退化为 `{attr, op}`（不符合 design 声明的 `{attr, op, value}` 形态），运行时该规则**静默永不匹配**（实测：实体属性满足/不满足均返回 []，无任何报错）——一个 spec 笔误即产出不可诊断的死规则，与 design「define 时校验 where 字段合法性」相悖。
- **materialize.props 含未定义属性**：apply 时经 `setProperty` 抛 `Attribute 'x' is not defined for type 'Y'`，错误可诊断（含属性名与类型名）；会遗留无边的部分物化 Skolem 实体，但修正规则后重跑因确定性 ID + upsert 语义**自愈**（实测：修复 props 后 created=1、fixpoint=true、全库仅 1 个目标实体）。可接受的 v1 行为，记录为观察项（见 §4）。

### 1.3 性能 / 规模角度 — 实现合理，但 design.md 有漂移（GAP，已修复文档）

- `_findExistentialViolations` 把每条规则编译为**一个**集合语义查询（body 集 − head 满足集，`not sat[id]` 引擎内求差；toType 闭包/rel 别名集以 `$to_types`/`$rel_names` 参数传入、`is_in` 单查询内判定），但**按 body 类型闭包逐类型名执行**（每个闭包成员+alias 一次查询）。这与 `findByType`/`aggregateByType` 的既有逐类型模式一致，查询次数与 schema 规模（类型闭包大小）成正比、与实例数无关——design 针对「大实例集」的风险缓解实质成立。
- 但 design.md 字面声明「每条规则编译为**一次**集合查询」「type 闭包在查询前用 JS 计算并以参数传入」与实际执行形态（body 闭包逐类型循环）不符，属文档漂移 → 已修订 design.md（见 §3）。未改实现：逐类型模式是仓库既定惯例，行为正确、性能特征可接受，改成单查询属无必要的行为面变更（created 顺序等会变化）。

### 1.4 文档一致性 — design 伪代码与 decisions 复核

- design.md chase 伪代码 `sha256(rule.name + '|' + v.entityId)` 与实现 `_skolemIdFor`（cozo-om.js:5397，`${ruleName}|${entityId}` 截 16 hex）一致；round 2 的 attemptKey `` 修复不出现在伪代码中（伪代码未涉及 attempted 集），无漂移。
- decisions.md 6 项决策与实现逐一吻合；analysis/findings 的复用清单（getDescendants、_runDslCreateIgnoreConflict、_replaceStoredRelation 等）均为实际复用路径。
- 除 §1.3 的「一次集合查询」表述外未见其他漂移。

### 1.5 spec_deltas ↔ 测试双向核对 — PASS（含 2 项记录性观察）

- 正向：25 个 case（OM-024×5、OM-025×6、OM-026×7、OM-027×4、VIZ-007×3）逐一确认仍有实质对应测试（define 13 例、check 6 例、temporal 6 例、chase 5 例、fixpoint 7 例、versioning 6 例、server 3 例、e2e 2 例），round 1-3 结论维持成立。
- 反向（实现了但 spec 未显式声明）：① `materialize.labelTemplate/props`——design §2 已声明、chase 测试覆盖，spec statement 为 requirement 级未列物化参数细节，认定为 design 级细节而非 spec gap；② `/integrity/seed-demo` 端点与 `invalidateAliasCache` 导出——round 1 已认定为演示便利/治理配套增量，维持认定；③ labelTemplate 的 `{fromId}`/`{rule}` 占位符语法仅在实现+测试中——同 ①。

## 2. Gap 列表

| # | 类别 | 内容 | 处置 |
|---|---|---|---|
| 1 | 实现校验（minor） | where 条目缺 value/undefined 被接受，JSON 持久化丢键产出「静默永不匹配」的死规则，不可诊断，违背 design 的 define 时校验声明 | `_normalizeExistentialSpec` 增加显式 value 必填校验（undefined 拒绝、null 合法）+ 回归测试 + spec delta case 措辞同步 |
| 2 | 文档漂移（minor） | design.md「每条规则编译为一次集合查询」与实际逐 body 类型执行不符 | 修订 design.md §3 与风险条目，如实描述逐类型执行与查询次数特征 |

## 3. 本轮修改清单

1. `cozo-lib-bun/cozo-om.js`：`_normalizeExistentialSpec` where 校验新增——`value` 键缺失或为 undefined 时抛 `forEach.where entries require an explicit value (attr 'x'); use null for a null comparison`（含注释说明 JSON 丢键根因）。行为变更仅为「原静默接受坏 spec → 现 define 时拒绝」，无既有合法用法受影响（全部既有测试零修改通过）。
2. `cozo-lib-bun/__tests__/om-existential-define.test.js`：新增测试 "rejects where entry without an explicit value (silent dead-rule guard)"（缺键/显式 undefined 双拒绝 + 拒绝不落库 + null 合法持久化）。
3. `codument/tracks/add-existential-rules/spec_deltas/cozo-om/delta.xml`：OM-024 `reject-invalid-spec` case 的 when 增补「或 where 条目缺少 value（undefined）」。
4. `codument/tracks/add-existential-rules/design.md`：§2 where 形态注明 value 必填语义；§3 与风险条目改为如实描述「集合语义查询、逐 body 类型名执行、toType/rel 以参数 is_in 判定、查询次数随 schema 而非实例数」。
5. plan.xml：无需修改（修复落在已 DONE 的 T1.2「spec 校验」范围内，无新增任务/AC 变化）。

## 4. 观察项（非 gap，留作后续增强候选）

- materialize.props 的属性键在 define 时不做存在性/canonicalize 校验（where.attr 做了 canonicalize）；运行时 `setProperty` 自行 alias 解析故重命名免疫（OM-027 满足），未定义属性在 apply 时报错可诊断且部分物化可自愈。若后续希望错误前移，可在 define 时对 props 键按 toType 做 resolve。
- 违例查询如需进一步压缩查询次数，可将 body 类型闭包并入 `is_in` 单查询（需注意 created 顺序兼容），与既有 findByType 模式的统一重构宜整库一起做。

## 5. 门控命令运行结果（修复后，全部通过）

| 命令 | 位置 | 结果 |
|---|---|---|
| `bun test` | 仓库根 | 242 pass / 0 fail（48 文件；241 既有 + 本轮新增 1） |
| `bun test --coverage` | cozo-lib-bun/ | 234 pass / 0 fail；cozo-om.js 行覆盖 91.46%；existential 段未覆盖仍仅 7 行防御性 throw/rethrow（5169/5174/5214/5218/5245/5253/5523，行号随新增校验整体下移），新增校验行已被测试覆盖，>80% 达标 |
| `codument validate --strict` | 仓库根 | 3 passed / 0 failed（track/add-existential-rules、spec/cozo-om、spec/cozo-lib-bun-viz） |
| `CI=true npm run test:e2e` | cozo-lib-bun-viz/ | 首跑 13 passed + 1 flaky（smoke.pw.ts:133 既有 permission demo 用例重试通过，round 2/3 已记录同源 flaky，与本 track 无关）；干净复跑 **14 passed，exit 0** |

## 6. 已确认设计取舍（复核，不算 gap）

v1 head 仅 exists:{rel,direction?,toType}；仅 maxIterations 兜底；`_skolem_rule` 内置属性标记；viz 用 governance 子 tab；前端页面内 postJson 而非 api.ts；integrity demo 独立 integrityDb —— 与 decisions.md 一致，维持既往认定。

## 7. 本轮动作与返回

- 修复 2 项 minor gap（1 实现 + 1 文档），同步 spec delta 措辞与 design.md；plan.xml 无需变更。
- 四项门控修复后全绿。
- 返回状态：FIX_APPLIED（不代表收口，是否再复检由父层决定）
