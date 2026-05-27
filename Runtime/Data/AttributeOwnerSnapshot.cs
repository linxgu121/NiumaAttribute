using System;

namespace NiumaAttribute.Data
{
    /// <summary>
    /// 单个 Actor 的存档快照。
    /// 只保存可恢复事实，不保存最终值缓存。
    /// </summary>
    [Serializable]
    public sealed class AttributeOwnerSnapshot
    {
        public string ActorId;
        public AttributeValueSnapshot[] Attributes = Array.Empty<AttributeValueSnapshot>();
        public ResourceValueSnapshot[] Resources = Array.Empty<ResourceValueSnapshot>();
        public AttributeModifierSnapshot[] PersistentModifiers = Array.Empty<AttributeModifierSnapshot>();
    }
}
