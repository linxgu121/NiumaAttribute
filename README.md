# NiumaAttribute

## 模块定位
NiumaAttribute 是属性与资源模块，负责角色基础属性、派生属性、资源上限、HP/MP/Stamina 等当前值、Modifier 分层计算、自动恢复、存档和属性 UI 数据。

## 框架设计思路
- 属性按 ActorId 隔离，后期单机 NPC、玩家、多玩家角色都使用同一套逻辑。
- 资源单独建模为 CurrentValue + MaxAttributeId，例如 HP 当前值读取 MaxHealth 作为上限。
- 所有加成通过 AttributeModifier 进入计算链，不直接改最终值。
- 效果生命周期属于 NiumaEffect，Attribute 只处理 Modifier 数值和基础恢复。

## 核心流程
1. AttributeController 加载 AttributeDefinition 和 ResourceDefinition。
2. SetBaseValue 写入基础值并触发重算。
3. AddModifier 按 SourceId + ModifierId 覆盖或新增修饰器。
4. Calculator 按 Base -> Derived -> Additive -> Percent -> Multiplier -> Clamp 计算最终值。
5. ResourceRuntimeUpdater 每帧处理基础自动恢复和资源 Clamp。
6. SaveAdapter 保存基础值、资源当前值和持久 Modifier。
7. UI Bridge 输出属性面板和资源条 ViewData。

## 模块用法
- ActorId 必须稳定，玩家、NPC、联机角色都应有唯一 ActorId。
- Modifier 需要填写 SourceId 和 ModifierId，避免重复叠加。
- 短期 Buff 建议由 NiumaEffect 管生命周期，再向 Attribute 添加/移除 Modifier。

## 场景使用方法
推荐放置方式：`AttributeRoot` 一个数值根物体承载属性服务、UI 桥接、存档；角色对象只放 ActorId 相关桥接。

- `AttributeRoot`：挂 `NiumaAttributeController`，绑定 AttributeDefinition、ResourceDefinition、默认 Actor 初始化配置。
- `AttributeRoot/SaveAdapter` 或全局 `SaveRoot/Providers`：挂 `NiumaAttributeSaveAdapter`。
- `AttributeRoot/UIBridge` 或 `UIRoot/Bridges`：挂 `AttributeUIViewBridge`，绑定属性面板 Receiver。
- `UIRoot/HpBar`、`UIRoot/StaminaBar`：挂 `AttributeResourceBarBridge`，填写 ActorId、ResourceId，例如 player / hp。
- `PlayerRoot/AttributeBridge`：挂 `TPCAttributeBridge`，用于把 TPC 的体力/生命逐步迁移或同步到 Attribute。
- `EquipmentRoot/AttributeBridge`：挂 `EquipmentAttributeModifierBridge`，把装备状态转成属性 Modifier 来源。
- `AttributeRoot/Debug`：开发阶段挂 `AttributeBasicTestRunner`。
- 多角色场景中不要复制 AttributeController；给每个角色分配不同 ActorId，由同一个 AttributeService 管理。

## 协作边界
Attribute 不做伤害公式、不做 Buff 生命周期、不做元素反应。Combat、Effect、Skill 可读取或修改属性事实，但不绕过 AttributeService。


