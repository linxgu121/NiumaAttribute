using System;
using NiumaAttribute.Enum;

namespace NiumaAttribute.Data
{
    /// <summary>
    /// 持久属性修饰器快照。
    /// 非持久修饰器默认不进入存档。
    /// </summary>
    [Serializable]
    public sealed class AttributeModifierSnapshot
    {
        public string SourceId;
        public string ModifierId;
        public string TargetAttributeId;
        public AttributeModifierOperation Operation;
        public AttributeModifierLayer Layer;
        public float Value;
        public int Priority;
        public float RemainingSeconds;
        public string SourceModule;

        public AttributeModifier ToModifier()
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
                IsPersistent = true,
                RemainingSeconds = RemainingSeconds,
                DurationSeconds = RemainingSeconds,
                SourceModule = SourceModule
            };
        }

        public static AttributeModifierSnapshot FromModifier(AttributeModifier modifier)
        {
            if (modifier == null)
            {
                return null;
            }

            return new AttributeModifierSnapshot
            {
                SourceId = modifier.SourceId,
                ModifierId = modifier.ModifierId,
                TargetAttributeId = modifier.TargetAttributeId,
                Operation = modifier.Operation,
                Layer = modifier.Layer,
                Value = modifier.Value,
                Priority = modifier.Priority,
                RemainingSeconds = modifier.RemainingSeconds,
                SourceModule = modifier.SourceModule
            };
        }
    }
}
