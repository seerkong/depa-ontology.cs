# REC-1 · Om.Core 补实体删除 API，Om.Depa 停止绕层直删（P0）

- 维度：Data（唯一写入者）+ 分层；裁定：GAP。
- 证据：DepaViolationDetectors.cs:726-732（原生 `:rm om_entity {id}`）；对照 :663-673 写入 11 个属性未随删（孤儿 om_property）；CozoOm.cs 无任何删除实体 API。
- 改什么：
  1. Om.Core/Logic/EntityLogic.cs 新增 `DeleteEntityAsync(runtime, entityId, ct)`：单事务内删 om_entity 行 + 该实体全部 om_property 行 + 相关 om_edge（或按时态语义 retract）；CozoOm.cs 加 facade 委托。
  2. Om.Depa/DepaViolationDetectors.cs:713-733 过期段改调 `om.DeleteEntityAsync(staleId)`，删除原生脚本。
- 验收：过期一条 depa_violation 后，`*om_property{entity_id: staleId}` @ NOW 无残留；ck_*/depa_* 现有测试全绿。
- 影响面：新增公开 API（非破坏）；需要新测试（删除语义 + 时态 retract 决策——retract 还是物理删，实现前需定一次）。
