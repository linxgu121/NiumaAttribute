using NiumaAttribute.Enum;

namespace NiumaAttribute.ViewData
{
    /// <summary>
    /// 单层属性修饰表现数据。
    /// 用于 UI 和调试面板展示属性最终值来自哪些层。
    /// </summary>
    public sealed class AttributeLayerValueViewData
    {
        public AttributeModifierLayer Layer;
        public float AddValue;
        public float PercentAddValue;
        public float MultiplierValue = 1f;
    }
}
