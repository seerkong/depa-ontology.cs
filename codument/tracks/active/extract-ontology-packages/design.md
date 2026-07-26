# 设计

`Depa.Datalog.Core` 提供解析、IR、诊断和校验，`Depa.Datalog.Cozo` 将 IR 编译为 CozoScript。`Depa.Ontology` 由 OM Core、Query、Batch 和 Analytics 组成，公开 `CozoOm` 并以 `Depa.Cozo` 适配真实存储；也可接受抽象 store 进行测试或替换。

YAML 与 Jint 只实现可选 portability/scripting adapter，因此作为独立包保留。所有项目在同一个 `.slnx` 中构建；开发时以本地 feed 模拟 `Depa.Cozo@0.1.0`，发布时解析公开 NuGet 依赖。

测试不再依赖旧单体目录。`Depa.Ontology.Tests` 引用拆分后的项目，并验证 Jint adapter 仍仅由可选包引用。薄绑定将 native asset 作为可复制内容保留在标准 `runtimes/<RID>/native/` 布局，使 ProjectReference 和 NuGet PackageReference 都可由同一 resolver 加载。

目录层次用 `src/` 表达全部可发布的生产项目，`tests/` 表达可执行测试项目。原大 `Program.cs` 不再作为跨领域入口：Datalog 解析/编译矩阵属于 `Depa.Datalog.Tests`，以数据库为中心的 OM、adapter、Batch/Analytics 矩阵属于 `Depa.Ontology.Tests`；知识库部分由知识库仓库承担。
