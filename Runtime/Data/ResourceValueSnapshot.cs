using System;

namespace NiumaAttribute.Data
{
    /// <summary>
    /// 资源当前值快照。
    /// 不保存 MaxValueCache，导入后由最大值属性重新计算。
    /// </summary>
    [Serializable]
    public sealed class ResourceValueSnapshot
    {
        public string ResourceId;
        public float CurrentValue;
    }
}
