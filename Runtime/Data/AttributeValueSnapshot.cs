using System;

namespace NiumaAttribute.Data
{
    /// <summary>
    /// 属性基础值快照。
    /// 不保存 FinalValue，导入后由服务层重新计算。
    /// </summary>
    [Serializable]
    public sealed class AttributeValueSnapshot
    {
        public string AttributeId;
        public float BaseValue;
    }
}
