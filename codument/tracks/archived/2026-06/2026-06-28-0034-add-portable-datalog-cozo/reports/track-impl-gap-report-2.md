# Gap Loop 报告：add-portable-datalog-cozo 第 2 轮

## 范围

- Track: `add-portable-datalog-cozo`
- Scope: `track`
- Round: `2`
- Mode: FIX recheck, lightweight incremental review

## 已读取输入

- `codument/std/operations/gap-loop.md`
- `codument/tracks/add-portable-datalog-cozo/reports/track-impl-gap-report-1.md`
- `codument/tracks/add-portable-datalog-cozo/track.xml`
- `cozo-lib-dotnet/packages/Datalog.Core/Lexer.cs`
- `cozo-lib-dotnet/packages/Datalog.Cozo/CozoCompiler.cs`
- `cozo-lib-dotnet/tests/Program.cs`
- 第 1 轮修复文件的当前 diff

## 复检焦点

本轮只复检第 1 轮的两个修复：

1. malformed `?`、`:` 和 `!` marker 不得让 parser recovery 挂住，并且必须返回 structured diagnostics。
2. 存在 backend error diagnostics 时，Cozo compiler 不得返回 partial executable script。

## 复检发现

### GAP-1 复检：malformed marker recovery

状态：已闭合。

`Lexer.Unknown(...)` 现在在进入恢复流程但尚未消费输入时，会把 `_position` 至少推进到 `start + 1`，保证向前进展。`?`、`:` 和 `!` 的 malformed marker 分支在没有匹配 `?-`、`:-` 或 `!=` 时都会调用 `Unknown(...)`，因此每个 malformed marker 都会先被消费，再继续恢复。

测试套件包含 `PortableDatalogParser.Parse("? calls(X).")`，并断言它以 diagnostic `DL1001` 失败，覆盖了原始挂住路径。

### GAP-2 复检：backend diagnostics 与 partial script emission

状态：已闭合。

`CozoDatalogCompiler.Compile(...)` 现在会在代码生成后复制 diagnostics，并在存在任何 severity 为 `Error` 的 diagnostic 时，让 `Script` 返回 `string.Empty`。这会阻止 stored relation arity mismatch diagnostic `CZ4002` 等 backend validation error 返回 partial executable CozoScript。

测试套件包含 invalid stored relation mapping，并断言 compile result 不成功且 script 为空。

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

结果：`add-portable-datalog-cozo: track.xml OK + 1 behavior delta(s)`。

## 裁决

`NO_GAP`

第 2 轮确认第 1 轮的两个 gap 均已闭合。本轮没有修改实现、track、behavior 或 design。
