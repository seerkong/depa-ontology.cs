# 变更：提取 .NET Datalog 与 ontology 包

## 背景和动机 (Context And Why)

`cozo-lib-dotnet` 同时承载 native binding、Datalog、对象模型和可选 adapter。native binding 已成为 `Depa.Cozo`，其余层需要独立发布与演进。

## "要做"和"不做" (Goals / Non-Goals)

**目标:**

- 以 `Depa.Datalog.Core`、`Depa.Datalog.Cozo`、`Depa.Ontology` 拆分原 `src/`。
- 保留 YAML 与 Jint 为可选 ontology adapter 包。
- 对 `Depa.Cozo@0.1.0` 使用 NuGet package dependency。

**非目标:**

- 不复制 native 库或发布 NuGet 包。

## 变更内容（What Changes）

- 建立多项目 solution、统一包元数据和 readme。
- 修复拆包后必须显式声明的 `Depa.Cozo` 与 `Depa.Ontology` 命名空间引用。

## 测试迁移记录

原 OM 契约 fixture、schema/permission/existential parity fixture 和 Jint contract 已迁至 `tests/Depa.Ontology.Tests`。测试以真实 `Depa.Cozo` ProjectReference 运行；RID native asset 同时在直接项目引用和打包后的 `PackageReference` 消费场景中得到验证。

所有可发布的 ontology 子项目（包括 YAML/Jint adapter）统一置于 `src/`。原单体 `Program.cs` 的 Portable Datalog parser/compiler 部分已拆至 `tests/Depa.Datalog.Tests`；其余 OM、行为、YAML、Batch 与 Analytics 部分归属 `tests/Depa.Ontology.Tests`。

## 影响范围（Impact）

- 受影响的能力（behaviors）：depa-ontology-dotnet
- 受影响的代码：src、packages、Depa.Ontology.slnx
