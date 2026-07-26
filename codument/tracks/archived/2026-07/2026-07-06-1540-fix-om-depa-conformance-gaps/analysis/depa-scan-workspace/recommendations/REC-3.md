# REC-3 · indexed_at 改走 TimeProvider（P2）

- 维度：Runtime 显式化（非确定性显式注入）；裁定：GAP（轻）。
- 证据：CozoOmCodeKnowledgeExtensions.cs:169 `DateTimeOffset.UtcNow.ToString("O")`；Om.Core 纪律对照 CozoOmRuntime.cs:16（Options.TimeProvider）与 EntityLogic.cs:60/64。
- 改什么：改为 `om.Runtime.Options.TimeProvider.GetUtcNow().ToString("O")`（一行）。顺带 grep 全库确认无其他 UtcNow 直取（本次扫描仅此一处）。
- 影响面：零 API 变化；索引测试可用 FakeTimeProvider 断言 indexed_at 变得可测。
