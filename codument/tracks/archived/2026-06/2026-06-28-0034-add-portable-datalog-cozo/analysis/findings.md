# 发现

## 已发现事实
- 工作区中已经存在 `cozo-lib-dotnet/src/Om.Core/`、`cozo-lib-dotnet/src/Om.Batch/` 和 `cozo-lib-dotnet/src/Om.Analytics/`。
- 进行中的 track `add-dotnet-viz-server` 将 `Om.Core` 作为本体核心，并把 batch/analytics 保持为 sibling capsule。
- CozoDB 的 product/project attractor 将 CozoScript/Datalog 识别为核心数据库能力。

## 约束
- `Datalog.Core` 不得依赖 Cozo runtime 类型或 `Om.Core`。
- `Datalog.Cozo` 可以依赖 `Datalog.Core`，但不得依赖 `Om.Core`。
- 两个 Datalog 模块都必须放在 `cozo-lib-dotnet/packages/` 下，并作为独立 `.csproj` 子项目存在。
- `Om.Query`、NamedQuery registry、代码/文档 ontology、wiki 编译都刻意排除在本 track 之外。
- track 产物必须自包含，不依赖隐藏目录中的文档才能理解。

## 未决问题
- `Datalog.Core` 和 `Datalog.Cozo` 后续是否要作为独立 NuGet 包打包/发布。
- stratified negation 在 v1 中是直接启用，还是先能解析但默认拒绝。

## 结论
- 在引入 `Om.Query` 和 `Om.CodeKnowledge` 前，backend-neutral 的 Portable Datalog 层是合适的底层基础。
- 第一版实现应优先选择小而可测试的 Datalog profile 和明确 diagnostics，而不是追求宽泛语言兼容。

## 实现证据
- 2026-06-27T16:20:51Z：已实现 `cozo-lib-dotnet/packages/Datalog.Core/Cozo.DotNet.Datalog.Core.csproj` 独立项目，包含 AST、parser、validator、normalized IR 和 diagnostics。该项目没有对 Cozo 或 Om 的项目引用。
- 2026-06-27T16:20:51Z：已实现 `cozo-lib-dotnet/packages/Datalog.Cozo/Cozo.DotNet.Datalog.Cozo.csproj` 独立项目，只引用 `../Datalog.Core/Cozo.DotNet.Datalog.Core.csproj`。
- 2026-06-27T16:20:51Z：已在 `cozo-lib-dotnet/tests/Program.cs` 增加 parser/validator/compiler 覆盖，并在 `cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj` 增加项目引用。
- 2026-06-27T16:20:51Z：已新增 `cozo-lib-dotnet/packages/README.md`，说明 package 边界，并给出 Portable Datalog 到 CozoScript 的示例。
- 验证已通过：
  - `dotnet build cozo-lib-dotnet/packages/Datalog.Core/Cozo.DotNet.Datalog.Core.csproj`
  - `dotnet build cozo-lib-dotnet/packages/Datalog.Cozo/Cozo.DotNet.Datalog.Cozo.csproj`
  - `dotnet run --project cozo-lib-dotnet/tests/Cozo.DotNet.Om.Tests.csproj`
  - `codument validate add-portable-datalog-cozo --strict`
- gap-loop 第 1 轮发现并修复两个 gap：`Lexer.Unknown(...)` 对 malformed marker 的恢复问题，以及 `CozoDatalogCompiler.Compile(...)` 在存在 backend error diagnostics 时仍返回 partial script 的问题。
- gap-loop 第 2 轮返回 `NO_GAP`；第 1 轮的两个修复均已通过 package build、测试运行和 codument validate 复检闭合。
