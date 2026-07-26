# scope — 本次范围

- 范围层级：module；深度：standard。
- In：cozo-lib-dotnet/src/Om.Core/、Om.CodeKnowledge/、Om.Depa/（同一程序集 Cozo.DotNet.csproj 下的三个目录级 capsule）。
- Out：Om.Analytics、Om.Batch、Om.Query（仅作为依赖方向观察）、CozoDb/CozoNative（vendor 边界）、tests/、任何 CLI/Wiki 包。超范围发现登记 recommendations/backlog.md。
- 关注点：
  1. V-F2 五条（OmMutationContext 承载方法，CozoOmRuntime.cs:121/124/127/130/133）真伪裁定。
  2. runtime-explicitness（CozoOm/CozoOmRuntime 注入显式性、Logic 类依赖来源）。
  3. capsule-protocol（入口/internals/契约边界；Om.Depa→Om.CodeKnowledge 单向）。
  4. fact-grade（ck_* 观测层 vs depa_* 派生层，唯一写入者）。
  5. component-protocol（Logic 抽查 2-3 个方法）。
  6. Effect 维（CozoDbOmStore 唯一副作用出口承诺）。
