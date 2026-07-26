# Findings

## Found Facts

- 当前 example-browser 默认调用 `http://127.0.0.1:4175` 的 demos、run、permission、schema 和 governance API。
- 已迁入的历史 server track 将 OM Core、Batch、Analytics 定义为可复用层，将 HTTP、demo 及 server state 定义为应用层。
- 当前 .NET contract executable 在缺少 `cozo_c.dylib` 时无法运行，但 solution 可编译。

## Constraints

- 仅兼容当前 TypeScript example-browser，不兼容历史 Bun viz frontend。
- native runtime 缺失必须显式暴露，不能被 mock 掩盖。

## Open Questions

- 无；用户已授权立即实现。

## Conclusions

- 新 server 应置于 examples，而非 src/packages，并通过 HTTP contract tests 固定浏览器兼容性。

## T1.1 — 2026-07-26

- 测试先行：新增的 HTTP contract executable 在 server project 尚不存在时无法还原其 ProjectReference；实现后以真实 loopback Kestrel host 验证了 `GET /health`、`GET /api/demos`、允许的 `127.0.0.1:4174` 预检，以及拒绝任意 origin 的预检。
- `dotnet build Depa.Ontology.slnx` 通过（0 warning、0 error）；`dotnet run --project examples/Depa.Ontology.ExampleServer.Tests --no-build` 通过。
- `Depa.Ontology` 原有的 `Depa.Cozo` 0.1.0 PackageReference 缺少源码已使用的 `CozoTransaction`。本地开发改为显式引用同级 `../../../cozo/depa-cozo-csharp/Depa.Cozo.csproj`，使迁移后的 binding 与 ontology 一致。发布时必须改回包含相同 API/native assets 的已发布 package。
- 当前 T1.1 只暴露不触碰 native runtime 的 skeleton endpoint；native `cozo_c` 的缺失不会伪造为通过，待 T2.1 创建内存数据库时必须以真实 smoke 验证。

## T2.1 — 2026-07-26

- `POST /api/run` 为每个已知 demo/query 建立并释放独立 `CozoDb("mem", "")`；每次调用都从请求 tables（或 catalog 默认 tables）重建 OM schema、batch data 与关系，因此请求之间不共享 demo state。
- 输出通过 `Depa.Ontology` 的 core、Batch 与 Analytics API 映射为 browser 的 `table`、`tree`、`graph` JSON shape；未知 demo 或 query 在创建 native 数据库前返回 HTTP 200 的 `{ status: "error", error }`。
- 真实 loopback HTTP contract 成功：六个默认 demo 各执行首个 query，另覆盖 procurement 的 graph 和 tree；错误路径也已覆盖。`dotnet build examples/Depa.Ontology.ExampleServer.Tests/Depa.Ontology.ExampleServer.Tests.csproj --no-restore` 为 0 warning/0 error，`dotnet run --project examples/Depa.Ontology.ExampleServer.Tests --no-build` 通过。
- 本机 native runtime 实际可加载，完整 run contracts 没有被跳过；测试保留 native 探测分支，缺失 `cozo_c` 的环境会明确跳过完整 run 契约而不 mock 成功结果。

## T2.2 — 2026-07-26

- 真实 loopback Kestrel contract 覆盖 schema state、apply、diff 和 rollback；迁移将版本从 1 推进至 2，rollback 恢复至 1。
- integrity contract 覆盖 `seed-demo → check → apply → check`：seed 产生两个未归属资源违例，apply 达到 fixpoint，随后 check 返回零违例；permission `seed` 与 `checkAccess` 的 browser envelope 同时已验证。
- state-isolation 由 HTTP 验证：integrity reset 后 schema 仍处于 v2，governance reset 后 integrity 仍保留两个违例；三个 state domain 的独立 async lock/内存数据库没有相互污染。
- `dotnet build examples/Depa.Ontology.ExampleServer.Tests/Depa.Ontology.ExampleServer.Tests.csproj --no-restore`、`dotnet run --project examples/Depa.Ontology.ExampleServer.Tests --no-build` 与 `dotnet build Depa.Ontology.slnx --no-restore` 均通过（0 warning、0 error）。

## T3.1 — 2026-07-26

- `examples/Depa.Ontology.ExampleServer/README.md` 已明确 example-browser 的双终端启动方式、`VITE_API_BASE=http://127.0.0.1:4175`、受限的 CORS origin 扩展方式，以及 `Depa.Ontology` 到同级 `depa-cozo-csharp` 项目引用和可加载 `cozo_c` 的前置条件。
- 独立验收通过：`dotnet build Depa.Ontology.slnx --configuration Release --no-restore` 为 0 warning、0 error；`dotnet run --project examples/Depa.Ontology.ExampleServer.Tests/Depa.Ontology.ExampleServer.Tests.csproj --configuration Release --no-build` 通过。后者以真实 loopback Kestrel 和本机可加载 native Cozo runtime 覆盖 demos、run、schema、governance、integrity 与 CORS，而非跳过 native path。
- `codument validate add-dotnet-example-server --strict` 通过。`codument modeling validate --deltas add-dotnet-example-server` 为 0 error、1 warning；warning 仅表示 modeling registry 尚为空，因此不需要 domain plane，和本 track 的有效 modeling delta 不冲突。
