# Gap Loop 报告：add-portable-datalog-cozo 第 1 轮

## 范围

- Track: `add-portable-datalog-cozo`
- Scope: `track`
- Round: `1`
- Mode: full target comparison

## 已读取输入

- `codument/tracks/add-portable-datalog-cozo/proposal.md`
- `codument/tracks/add-portable-datalog-cozo/design.md`
- `codument/tracks/add-portable-datalog-cozo/behavior_deltas/dotnet-portable-datalog/delta.xml`
- `codument/tracks/add-portable-datalog-cozo/track.xml`
- `codument/tracks/add-portable-datalog-cozo/analysis/findings.md`
- 本轮前不存在历史 gap 报告。

## 已审查实现

- `cozo-lib-dotnet/packages/Datalog.Core/**`
- `cozo-lib-dotnet/packages/Datalog.Cozo/**`
- `cozo-lib-dotnet/packages/README.md`
- `cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj`
- `cozo-lib-dotnet/tests/Program.cs` 中的 Datalog 测试块

## 目标对比

### 已满足

- `Datalog.Core` 已作为独立项目存在于 `cozo-lib-dotnet/packages/Datalog.Core/Cozo.DotNet.Datalog.Core.csproj`。
- `Datalog.Core` 提供 AST、parser、validator、normalized IR，以及包含 severity/code/message/span 的 diagnostics。
- `Datalog.Core` 没有对 Cozo、`CozoDb` 或 `Om.Core` 的项目引用。
- `Datalog.Cozo` 已作为独立项目存在于 `cozo-lib-dotnet/packages/Datalog.Cozo/Cozo.DotNet.Datalog.Cozo.csproj`。
- `Datalog.Cozo` 只引用 `Datalog.Core`，不引用 `Cozo.DotNet.csproj` 或 `Om.Core`。
- 实现支持 recursive rules、query atoms、parameters、constants、comparisons、named-field stored relation mappings、parameter preservation、string escaping 和 diagnostics。
- `cozo-lib-dotnet/packages/README.md` 记录了 package 边界，并包含 Portable Datalog 到 CozoScript 的示例。
- 测试覆盖 parser/validator/compiler happy path、unsafe variables、parameter preservation、mapping、malformed parser diagnostics 和 backend diagnostic behavior。

### 已发现并修复的 gap

#### GAP-1：parser 对 malformed `?`、`:` 或 `!` 的错误恢复可能挂住

lexer 在某些分支遇到 malformed one-character marker 时会返回 `Unknown(...)`，但没有先推进输入位置。这会导致 `? calls(X).` 这类 malformed input 在没有消费输入的情况下递归恢复，而不是返回结构化 diagnostic。这违反了 malformed Portable Datalog 的 source diagnostics 要求。

已应用修复：

- 更新 `cozo-lib-dotnet/packages/Datalog.Core/Lexer.cs`，确保 `Unknown(...)` 在继续恢复前至少消费一个字符。
- 在 `cozo-lib-dotnet/tests/Program.cs` 中增加测试，断言 malformed query marker 输入返回 diagnostic `DL1001`，且不会挂住。

#### GAP-2：存在 backend diagnostics 时，Cozo compiler 仍可能输出 partial executable script

behavior delta 要求 unsupported/backend capability 错误返回结构化 diagnostics，并且不输出 partial executable CozoScript。compiler 对 stored relation arity mismatch 这类 invalid backend condition 记录了 diagnostics，但仍返回生成的脚本文本。

已应用修复：

- 更新 `cozo-lib-dotnet/packages/Datalog.Cozo/CozoCompiler.cs`，让 `Compile(...)` 在存在任何 error diagnostic 时返回空脚本。
- 在 `cozo-lib-dotnet/tests/Program.cs` 中增加测试，断言 invalid stored relation mapping 会产生失败结果和空脚本。

## 验证

已通过：

```bash
dotnet build cozo-lib-dotnet/packages/Datalog.Core/Cozo.DotNet.Datalog.Core.csproj
```

结果：success，0 warnings，0 errors。

已通过：

```bash
dotnet build cozo-lib-dotnet/packages/Datalog.Cozo/Cozo.DotNet.Datalog.Cozo.csproj
```

结果：success，0 warnings，0 errors。

已通过：

```bash
dotnet run --project cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj
```

结果：`Cozo.DotNet OM tests passed.`

已通过：

```bash
codument validate add-portable-datalog-cozo --strict
```

结果：`track.xml OK + 1 behavior delta(s)`。

## 裁决

`FIX_APPLIED`

第 1 轮发现两个实现 gap，并已在请求的实现范围内修复。按 gap-loop 协议，需要再启动一轮 fresh 复检，聚焦本轮修复范围。
