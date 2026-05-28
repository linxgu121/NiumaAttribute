using System;
using UnityEngine;

namespace NiumaAttribute.EquipmentBridge
{
    /// <summary>
    /// 装备属性加成映射表。
    /// 通过独立 Profile 配置 EquipmentId -> Attribute Modifier，避免 Equipment 核心模块直接依赖 Attribute。
    /// </summary>
    [CreateAssetMenu(menuName = "NiumaAttribute/Bridge/Equipment Modifier Profile", fileName = "EquipmentAttributeModifierProfile")]
    public sealed class EquipmentAttributeModifierProfile : ScriptableObject
    {
        [Tooltip("装备属性加成映射列表。正式内容中 EquipmentId 不应重复。")]
        public EquipmentAttributeModifierEntry[] Entries = Array.Empty<EquipmentAttributeModifierEntry>();

        /// <summary>
        /// 尝试获取某个装备定义对应的属性加成规则。
        /// 第一版使用线性查找，配置量通常很小；后续大量装备时可在桥接器侧缓存索引。
        /// </summary>
        public bool TryGetModifiers(string equipmentId, out EquipmentAttributeModifierRule[] modifiers)
        {
            modifiers = Array.Empty<EquipmentAttributeModifierRule>();
            if (string.IsNullOrWhiteSpace(equipmentId) || Entries == null)
            {
                return false;
            }

            for (var i = 0; i < Entries.Length; i++)
            {
                var entry = Entries[i];
                if (entry == null || !string.Equals(entry.EquipmentId, equipmentId, StringComparison.Ordinal))
                {
                    continue;
                }

                modifiers = entry.Modifiers ?? Array.Empty<EquipmentAttributeModifierRule>();
                return modifiers.Length > 0;
            }

            return false;
        }
    }
}
