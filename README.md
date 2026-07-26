# Depa Ontology for .NET

本仓库是 .NET 的多包工作区，不包含 native database binding：`Depa.Ontology`
通过 NuGet 依赖 `Depa.Cozo@0.1.0`。

| 包 | 职责 |
| --- | --- |
| `Depa.Datalog.Core` | Datalog parser、validator、IR 与 diagnostics |
| `Depa.Datalog.Cozo` | 将 IR 编译为 CozoScript |
| `Depa.Ontology` | 对象模型与本体运行时 |
| `Depa.Ontology.Portability.Yaml` | 可选 YAML manifest adapter |
| `Depa.Ontology.Scripting.Jint` | 可选 Jint callback adapter |

发布顺序是 `Depa.Datalog.Core`、`Depa.Datalog.Cozo`、`Depa.Ontology`，最后才是可选 adapter。开发验证可将本地 `Depa.Cozo.0.1.0.nupkg` 放入 NuGet source 后运行：

所有可发布项目位于 `src/`；测试按归属拆为 `tests/Depa.Datalog.Tests` 与 `tests/Depa.Ontology.Tests`。

```bash
dotnet build Depa.Ontology.slnx --configuration Release
dotnet pack Depa.Ontology.slnx --configuration Release
```
