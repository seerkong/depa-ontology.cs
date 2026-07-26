# Design

## 方案

使用 ASP.NET Core minimal API 新建私有示例应用。路由层只处理 JSON、HTTP 状态和 CORS；demo seeding、schema/governance/integrity 状态及结果映射封装在应用内部的服务类型中。

`/api/run` 为每个请求创建并释放独立的内存 Cozo database。schema、governance 和 integrity 各自使用独立的 state domain；reset/seed 操作通过域内异步 gate 串行化，避免请求间互相释放或重置数据库。

## 合约策略

以当前浏览器使用的 `Demo`、`RunResponse` 与 governance/schema endpoint JSON shape 为兼容边界。测试通过真实 HTTP host 验证端点、CORS 预检、demo result 的 table/tree/graph shape，以及治理与存在规则流程。

## 风险与缓解

- native `cozo_c` 不可加载：启动 smoke 和 README 明确运行前置条件；CI 将此视为环境失败，不伪造通过结果。
- demo 夹具在双语言实现间漂移：保留明确的端点契约测试和固定 demo id/query id 覆盖。
- 有状态治理请求并发 reset：按 state domain 串行化，避免共享数据库被请求中途替换。

## 兼容与部署

默认仅允许 `http://localhost:4174` 和 `http://127.0.0.1:4174`；可由配置扩展。前端仍由 Vite 托管，并通过 `VITE_API_BASE` 指向 server。
