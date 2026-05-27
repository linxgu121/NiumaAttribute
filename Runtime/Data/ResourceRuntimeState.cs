using System;

namespace NiumaAttribute.Data
{
    /// <summary>
    /// 单个资源运行时状态。
    /// CurrentValue 进入存档，MaxValueCache 和 RecoveryDelayTimer 是运行时缓存。
    /// </summary>
    [Serializable]
    public sealed class ResourceRuntimeState
    {
        public string ResourceId;
        public string MaxAttributeId;
        public float CurrentValue;
        public float MaxValueCache;
        public float RecoveryDelayTimer;
        public long Revision;

        public ResourceValueSnapshot ToSnapshot()
        {
            return new ResourceValueSnapshot
            {
                ResourceId = ResourceId,
                MaxAttributeId = MaxAttributeId,
                CurrentValue = CurrentValue
            };
        }

        public static ResourceRuntimeState FromSnapshot(ResourceValueSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            return new ResourceRuntimeState
            {
                ResourceId = snapshot.ResourceId,
                MaxAttributeId = snapshot.MaxAttributeId,
                CurrentValue = snapshot.CurrentValue
            };
        }
    }
}
