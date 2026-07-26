# 知识上下文

## 来源记录
| 来源 | 摘要 | 相关性 |
|--------|---------|-----------|
| `codument/attractors/project.md` | CozoDB 是面向 Datalog 的嵌入式数据库，具备多语言 binding，并在 Bun/.NET 方向包含 OM 能力。 | 确认 Datalog 是项目的一等方向。 |
| `codument/attractors/product.md` | 产品价值包含用于图与递归工作流的 Datalog/CozoScript。 | 支撑 Datalog-first 查询 API，而不是用 Cypher/SPARQL 替代。 |
| `codument/tracks/add-dotnet-viz-server/*` | 当前 .NET 重构创建 `Om.Core`、`Om.Batch`、`Om.Analytics`，并隔离 server/demo concerns。 | 确立新语言 capsule 的模块边界。 |

## 代码库知识
- 当前 .NET 主 binding 代码组织在 `cozo-lib-dotnet/src/` 下。
- 既有 OM 工作采用 sibling capsule 目录，而不是单体 `Om` 目录。
- 测试位于 `cozo-lib-dotnet/tests/`。

## 领域知识
- Portable Datalog 应被视为语言前端和 IR，而不是 OM 专属查询 API。
- CozoScript 应被视为 Portable Datalog compiler 的执行后端目标。
- 面向 agent 的查询层后续可以在此基础上通过 NamedQuery 和领域 facade 构建。

## 术语
| 术语 | 含义 |
|------|---------|
| Portable Datalog | 一个受限且 backend-neutral 的 Datalog profile，用于 AI/agent 编写查询并确定性编译。 |
| Datalog.Core | 位于 `cozo-lib-dotnet/packages/Datalog.Core/` 的独立项目，负责纯 parser、validator、AST、normalized IR 和 diagnostics。 |
| Datalog.Cozo | 位于 `cozo-lib-dotnet/packages/Datalog.Cozo/` 的独立项目，负责将 Portable Datalog IR 编译为参数化 CozoScript。 |
| Capability check | 校验步骤，用于拒绝所选 backend/profile 不支持的 construct。 |
