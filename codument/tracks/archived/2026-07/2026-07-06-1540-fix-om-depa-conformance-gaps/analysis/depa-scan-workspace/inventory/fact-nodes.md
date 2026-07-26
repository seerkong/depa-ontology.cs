# fact-nodes — runtime/input/config 归位 + component-protocol 抽查

## runtime 归位（Om.Core）

| 字段 | 角色 | 裁定 | 证据 |
|------|------|------|------|
| CozoOmRuntime.Store | 副作用契约（ICozoOmStore） | ✅ | CozoOmRuntime.cs:9 |
| CozoOmRuntime.Options（DefaultMaxChaseIterations/TimeProvider） | 静态配置 options | ✅（TimeProvider 归 runtime，非确定性显式化） | CozoOmRuntime.cs:13-17 |
| CozoOmRuntime.Registry | 应用作用域可变资源（validators/computed/mutations/actions/interceptors 五张注册表） | ✅ | CozoOmRuntime.cs:19-26 |
| Inputs/*（EntityInput/SetPropertyInput/…） | input：单次 payload，全 sealed record，零对象图、零 Callable | ✅ | OmInputs.cs:6-76 |
| WriteOptions/FindByTypeOptions/HistoryRangeOptions | 单次调用 config（布尔/字符串开关） | ✅ | OmInputs.cs:35-39 |
| DepaScanOptions(MapPath, EffectsPath) | ⚠️ config 携带文件路径，core 自行 IO 解析 | GAP（I-3） | DepaScanModels.cs:11-15 |

红灯逐条：runtime=Any？否（强类型 record）。config 塞函数？否（注册回调走 Registry，属 runtime，归位正确）。config 重复 runtime 字段？否。core 现取全局/env？否（grep 无 global/env/单例；唯一非确定性直取=I-4）。

## component-protocol 抽查（3 处）

| 方法 | 形态 | 裁定 |
|------|------|------|
| EntityLogic.SetPropertyAsync(runtime, SetPropertyInput, ct) | output=fn(runtime,input)；依赖全来自 runtime.Store/Options.TimeProvider；类型校验→写→约束校验一条链 | ✅ PASS（EntityLogic.cs:50-111） |
| ConstraintLogic.ExecuteActionAsync(runtime, …) | 开事务后 `runtime with { Store = tx }` 重绑定副作用契约下传——runtime 不可变、按需派生新快照，教科书式 | ✅ PASS（ConstraintLogic.cs:71-84；ExecuteMutationsAsync 同型 :116-127；未知 mutation 显式 throw :142-145） |
| DepaScanPipeline.ScanAsync(om, options, ct) | 编排器：首参 CozoOm（runtime 持有者，等价形态）；内部局部闭包（UpsertAsync/AnnotateAsync…）承载跨步骤可变上下文（materialized/linked/bySymbolId 局部字典，不逃逸） | ✅ PASS（DepaScanPipeline.cs:47-436）；File IO 例外记 I-3 |

## OmMutationContext 归位

- OmMutationContext(Runtime, EntityId, TypeName)：请求级上下文 record + 5 个纯委托便捷方法（CozoOmRuntime.cs:119-135）；OmActionContext(:137-143) 继承并加 Params。角色=回调作用域 facade，非 runtime 载体。裁定见 incidents.md I-1（误报）。
