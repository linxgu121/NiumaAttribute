using System;

namespace NiumaAttribute.Data
{
    /// <summary>
    /// 单个 Actor 的属性容器。
    /// 玩家、NPC、远端玩家都应拥有独立 ActorId 和独立容器。
    /// </summary>
    [Serializable]
    public sealed class AttributeOwnerRuntimeState
    {
        public string ActorId;
        public AttributeRuntimeState[] Attributes = Array.Empty<AttributeRuntimeState>();
        public ResourceRuntimeState[] Resources = Array.Empty<ResourceRuntimeState>();
        public AttributeModifier[] Modifiers = Array.Empty<AttributeModifier>();
        public long Revision;

        public AttributeOwnerSnapshot ToSnapshot()
        {
            return new AttributeOwnerSnapshot
            {
                ActorId = ActorId,
                Attributes = AttributeSnapshotUtility.ToAttributeSnapshots(Attributes),
                Resources = AttributeSnapshotUtility.ToResourceSnapshots(Resources),
                PersistentModifiers = AttributeSnapshotUtility.ToPersistentModifierSnapshots(Modifiers)
            };
        }
    }
}
