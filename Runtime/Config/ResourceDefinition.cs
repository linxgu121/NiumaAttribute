using NiumaAttribute.Enum;
using UnityEngine;

namespace NiumaAttribute.Config
{
    /// <summary>
    /// 资源定义。
    /// 资源表示 HP、MP、Stamina 等有当前值和最大值的动态数值。
    /// </summary>
    [CreateAssetMenu(menuName = "NiumaAttribute/Resource Definition", fileName = "ResourceDefinition")]
    public sealed class ResourceDefinition : ScriptableObject
    {
        [Tooltip("资源稳定 ID。例如 health、mp、stamina。")]
        public string ResourceId;

        [Tooltip("资源分类。用于 UI、调试和后续模块筛选。")]
        public ResourceKind Kind = ResourceKind.Consumable;

        [Tooltip("资源显示名称。")]
        public string DisplayName;

        [Tooltip("资源说明。")]
        [TextArea]
        public string Description;

        [Tooltip("最大值绑定的属性 ID。例如 max_health。")]
        public string MaxAttributeId;

        [Tooltip("初始化当前值的方式。")]
        public ResourceInitialMode InitialMode = ResourceInitialMode.Full;

        [Tooltip("InitialMode 为 FixedValue 或 Percent 时使用。Percent 模式建议填 0 到 1。")]
        public float InitialValue;

        [Tooltip("是否进入存档。HP、MP、Stamina 通常需要保存。")]
        public bool SaveCurrentValue = true;

        [Tooltip("是否启用基础自动恢复。药水、中毒等效果恢复不要放这里，应由 NiumaEffect 处理。")]
        public bool EnableAutoRecovery;

        [Tooltip("每秒基础自动恢复量。")]
        public float RecoveryPerSecond;

        [Tooltip("资源被消耗后延迟多少秒开始基础自动恢复。")]
        public float RecoveryDelaySeconds;

        [Tooltip("基础自动恢复是否允许超过最大值。第一版建议保持关闭。")]
        public bool AllowOverRecover;
    }
}
