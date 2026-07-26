# 变更：新增 .NET 示例 API Server

## 背景和动机

`depa-ontology.ts/packages/example-browser` 当前依赖同级 TypeScript 示例 server。为使 .NET 包可独立演示其对象模型、Datalog、治理与存在规则能力，需要提供与该浏览器兼容的 ASP.NET Core API server。

## 目标

- 新增不发布为 NuGet 包的 ASP.NET Core 示例应用。
- 兼容当前 example-browser 使用的 demo、运行、权限、schema、governance 与 integrity HTTP 合约。
- 复用 `Depa.Ontology` 的 OM Core、Batch 和 Analytics；HTTP、demo 夹具和 server 生命周期留在应用层。
- 提供跨端口浏览器开发所需的受控 CORS 配置与可运行的契约测试。

## 非目标

- 不兼容历史 `cozo-lib-bun-viz/frontend`。
- 不托管前端静态资源，不发布 server NuGet 包。
- 不将 HTTP DTO、demo 数据或 Web hosting 逻辑下沉到 `Depa.Ontology`。

## 影响

- 新增 `examples/Depa.Ontology.ExampleServer` 和对应测试项目。
- solution 增加 examples 文件夹中的项目。
- 运行 server 仍需要可加载的 Cozo native library；这不由 server 静默绕过。
