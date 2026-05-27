using System;
using NiumaAttribute.Enum;

namespace NiumaAttribute.ViewData
{
    /// <summary>
    /// 单个属性 UI 表现数据。
    /// </summary>
    public sealed class AttributeValueViewData
    {
        public string ActorId;
        public string AttributeId;
        public string DisplayName;
        public AttributeKind Kind;
        public AttributeModifierLayer DominantLayer;
        public float BaseValue;
        public float FinalValue;
        public float ModifierDelta;
        public AttributeLayerValueViewData[] Layers = Array.Empty<AttributeLayerValueViewData>();
        public bool IsMissingDefinition;
    }
}
