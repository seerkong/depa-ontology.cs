# Knowledge Context

## Source Notes

| Source | Summary | Relevance |
|---|---|---|
| current example-browser | 定义浏览器消费的 API DTO 和端点 | HTTP compatibility boundary |
| current example-server | 提供 demo、schema、governance 与 integrity 行为 | behavioral reference |
| historical .NET viz server | 提供 ASP.NET minimal API 与 OM adapter 的迁移基础 | implementation reference |

## Codebase Knowledge

- `Depa.Ontology` 包含 CozoOm facade、Batch、Analytics、schema、permission 与 existential-rule API。
- `Depa.Cozo` 是 native binding 依赖，示例 server 需要直接创建内存 CozoDb。

## Terms

| Term | Meaning |
|---|---|
| example server | 为 Vue example-browser 提供 JSON API 的私有 ASP.NET 应用 |
| demo run | 基于浏览器表格输入 seed 内存数据库并返回 table/tree/graph 结果 |
