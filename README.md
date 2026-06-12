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

## 场景挂载与 Inspector 配置
### NiumaAttributeController
建议挂载位置：`CoreScene/BootstrapRoot/GameplayServicesRoot/AttributeRoot`。

用途：创建并注册全局属性服务，管理角色属性、资源当前值、Modifier 和自动恢复。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Attribute Definitions` | 拖属性定义资产，例如力量、体质、最大生命、最大体力 | 不建议 | 没有定义的属性无法查询和计算 |
| `Resource Definitions` | 拖资源定义资产，例如 HP、MP、Stamina | 不建议 | 对应资源无法消耗、恢复或存档 |
| `Register Service To Context` | 核心场景中开启 | 可以关闭 | 关闭后其他模块无法通过 GameContext 获取属性服务 |
| `Drive Tick In Update` | 没有统一模块启动器时开启 | 按项目决定 | 如果外部已经统一 Tick，再开启会造成重复 Tick |
| `Log Warnings` | 调试期建议开启 | 可以 | 关闭后配置缺失不提示 |

### NiumaAttributeSaveAdapter
建议挂载位置：`CoreScene/BootstrapRoot/SaveRoot/SaveAdapters`。

用途：把属性基础值、资源当前值、持久 Modifier 写入 NiumaSave。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Attribute Controller` | 拖 `NiumaAttributeController` | 不建议 | 自动查找失败时不会参与存档 |
| `Save Controller` | 拖 `NiumaSaveController` | 不建议 | 无法注册为存档 Provider |
| `Auto Find References` | 测试可开，正式建议手动绑定 | 可以 | 关闭且未绑定时适配器不工作 |

### AttributeUIViewBridge / AttributeResourceBarBridge
建议挂载位置：属性面板 UI 或血条 UI 所在物体。

用途：把属性服务数据转换成 UI ViewData，不直接制作 UI。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Attribute Controller` | 拖核心场景的 `NiumaAttributeController` | 不建议 | 自动查找失败时 UI 不刷新 |
| `Actor Id` | 填角色稳定 ID，例如 `player`、`npc_001` | 不可以 | 不知道显示谁的属性 |
| `Receiver Provider` | 拖实现属性面板/资源条接收的 UI 脚本 | 不可以 | 数据生成了但没有 UI 接收 |

### TPCAttributeBridge
建议挂载位置：`PlayerRoot/AttributeBridge`。

用途：把 TPC 的生命/体力逐步同步到 NiumaAttribute。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Player Controller` | 拖玩家的 `PlayerModuleController` | 不建议 | 自动查找失败时无法同步 TPC |
| `Attribute Controller` | 拖核心场景的 `NiumaAttributeController` | 不建议 | 无法写入属性资源 |
| `Actor Id` | 填玩家稳定 ID，建议 `player` | 不可以 | 属性模块无法区分角色 |



### UI Toolkit 接入

建议挂载位置：

- AttributeUIViewBridge：挂在属性面板桥接物体，例如 UIRoot/UIBridges/AttributeUIViewBridge。
- AttributeToolkitReceiver：挂在 UIRoot/UIBridges/AttributeToolkitReceiver，并拖到 AttributeUIViewBridge.Attribute UI Receiver Provider。
- AttributeToolkitBindingProvider：挂在 UIRoot/UIToolkitRoot/BindingProviders/AttributeBindingProvider，并拖到 UIToolkitViewFactory.Binding Provider Behaviours。
- AttributeResourceBarBridge：每一条 HP/MP/Stamina 资源条各挂一个。
- AttributeResourceBarToolkitReceiver：挂在对应资源条桥接物体上，并拖到 AttributeResourceBarBridge.Resource Bar Receiver Provider。
- AttributeResourceBarToolkitBindingProvider：挂在 UIRoot/UIToolkitRoot/BindingProviders/AttributeResourceBarBindingProvider。

默认 ViewId / BindingProviderId：

| 面板 | Receiver | Provider | ViewId / BindingProviderId |
| --- | --- | --- | --- |
| 属性面板 | AttributeToolkitReceiver | AttributeToolkitBindingProvider | AttributePanel |
| 资源条 | AttributeResourceBarToolkitReceiver | AttributeResourceBarToolkitBindingProvider | AttributeResourceBar |

属性面板 UXML 建议包含：TitleText、StatusText、ListRoot、DetailText、ResultText、EmptyRoot。资源条 UXML 建议包含：TitleText、ValueText、FillElement、EmptyRoot。
