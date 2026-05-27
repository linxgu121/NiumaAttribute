using System;
using System.Collections.Generic;

namespace NiumaAttribute.Data
{
    /// <summary>
    /// 属性快照转换工具。
    /// 只做显式字段映射，不直接序列化运行时状态。
    /// </summary>
    public static class AttributeSnapshotUtility
    {
        public static AttributeValueSnapshot[] ToAttributeSnapshots(AttributeRuntimeState[] states)
        {
            if (states == null || states.Length == 0)
            {
                return Array.Empty<AttributeValueSnapshot>();
            }

            var result = new List<AttributeValueSnapshot>(states.Length);
            for (var i = 0; i < states.Length; i++)
            {
                var snapshot = states[i]?.ToSnapshot();
                if (snapshot != null)
                {
                    result.Add(snapshot);
                }
            }

            return result.Count > 0 ? result.ToArray() : Array.Empty<AttributeValueSnapshot>();
        }

        public static ResourceValueSnapshot[] ToResourceSnapshots(ResourceRuntimeState[] states)
        {
            if (states == null || states.Length == 0)
            {
                return Array.Empty<ResourceValueSnapshot>();
            }

            var result = new List<ResourceValueSnapshot>(states.Length);
            for (var i = 0; i < states.Length; i++)
            {
                var snapshot = states[i]?.ToSnapshot();
                if (snapshot != null)
                {
                    result.Add(snapshot);
                }
            }

            return result.Count > 0 ? result.ToArray() : Array.Empty<ResourceValueSnapshot>();
        }

        public static AttributeModifierSnapshot[] ToPersistentModifierSnapshots(AttributeModifier[] modifiers)
        {
            if (modifiers == null || modifiers.Length == 0)
            {
                return Array.Empty<AttributeModifierSnapshot>();
            }

            var result = new List<AttributeModifierSnapshot>(modifiers.Length);
            for (var i = 0; i < modifiers.Length; i++)
            {
                var modifier = modifiers[i];
                if (modifier == null || !modifier.IsPersistent)
                {
                    continue;
                }

                var snapshot = AttributeModifierSnapshot.FromModifier(modifier);
                if (snapshot != null)
                {
                    result.Add(snapshot);
                }
            }

            return result.Count > 0 ? result.ToArray() : Array.Empty<AttributeModifierSnapshot>();
        }
    }
}
