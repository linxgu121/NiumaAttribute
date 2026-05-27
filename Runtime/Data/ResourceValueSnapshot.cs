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

        /// <summary>
        /// 导出时绑定的最大值属性 ID。
        /// 服务层导入时应优先使用当前 ResourceDefinition 刷新，这里用于迁移校验和配置缺失兜底。
        /// </summary>
        public string MaxAttributeId;

        public float CurrentValue;
    }
}
