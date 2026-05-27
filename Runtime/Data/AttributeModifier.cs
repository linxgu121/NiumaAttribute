using System;
using NiumaAttribute.Enum;

namespace NiumaAttribute.Data
{
    /// <summary>
    /// 属性修饰器。
    /// 这是 Attribute 的输入协议，复杂效果、元素反应和叠层规则由 NiumaEffect 负责裁决。
    /// </summary>
    [Serializable]
    public sealed class AttributeModifier
    {
        /// <summary>
        /// 来源实例 ID，例如 equipment:xxx、effect:xxx、story:xxx。
        /// </summary>
        public string SourceId;

        /// <summary>
        /// 来源内部唯一修饰器 ID。同一 SourceId + ModifierId 会互相覆盖。
        /// </summary>
        public string ModifierId;

        /// <summary>
        /// 目标属性 ID。
        /// </summary>
        public string TargetAttributeId;

        /// <summary>
        /// 运算类型。
        /// </summary>
        public AttributeModifierOperation Operation = AttributeModifierOperation.None;

        /// <summary>
        /// 计算分层。
        /// </summary>
        public AttributeModifierLayer Layer = AttributeModifierLayer.None;

        /// <summary>
        /// 修饰器数值。
        /// </summary>
        public float Value;

        /// <summary>
        /// 同层同运算下的排序优先级。数值越小越先计算。
        /// </summary>
        public int Priority;

        /// <summary>
        /// 是否进入存档。
        /// </summary>
        public bool IsPersistent;

        /// <summary>
        /// 持续时间。小于等于 0 表示永久。
        /// </summary>
        public float DurationSeconds;

        /// <summary>
        /// 剩余时间。由服务层维护，外部创建时可以不填。
        /// </summary>
        public float RemainingSeconds;

        /// <summary>
        /// 来源模块名，仅用于调试和日志。
        /// </summary>
        public string SourceModule;

        /// <summary>
        /// 创建浅拷贝，避免外部修改传入对象影响运行时状态。
        /// </summary>
        public AttributeModifier Clone()
        {
            return new AttributeModifier
            {
                SourceId = SourceId,
                ModifierId = ModifierId,
                TargetAttributeId = TargetAttributeId,
                Operation = Operation,
                Layer = Layer,
                Value = Value,
                Priority = Priority,
                IsPersistent = IsPersistent,
                DurationSeconds = DurationSeconds,
                RemainingSeconds = RemainingSeconds,
                SourceModule = SourceModule
            };
        }
    }
}
