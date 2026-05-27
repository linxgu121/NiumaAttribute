using System;

namespace NiumaAttribute.Data
{
    /// <summary>
    /// 单个属性运行时状态。
    /// FinalValue 是缓存，导入存档后必须根据配置和修饰器重新计算。
    /// </summary>
    [Serializable]
    public sealed class AttributeRuntimeState
    {
        public string AttributeId;
        public float BaseValue;
        public float FinalValue;
        public long Revision;

        public AttributeValueSnapshot ToSnapshot()
        {
            return new AttributeValueSnapshot
            {
                AttributeId = AttributeId,
                BaseValue = BaseValue
            };
        }

        public static AttributeRuntimeState FromSnapshot(AttributeValueSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            return new AttributeRuntimeState
            {
                AttributeId = snapshot.AttributeId,
                BaseValue = snapshot.BaseValue
            };
        }
    }
}
