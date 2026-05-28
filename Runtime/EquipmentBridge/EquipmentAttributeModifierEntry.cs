using System;
using UnityEngine;

namespace NiumaAttribute.EquipmentBridge
{
    /// <summary>
    /// 某个 EquipmentId 对应的一组属性加成规则。
    /// </summary>
    [Serializable]
    public sealed class EquipmentAttributeModifierEntry
    {
        [Tooltip("装备定义 ID，需要与 NiumaEquipment 的 EquipmentDefinition.EquipmentId 保持一致。")]
        public string EquipmentId;

        [Tooltip("该装备穿戴后输出到 Attribute 的修饰器列表。")]
        public EquipmentAttributeModifierRule[] Modifiers = Array.Empty<EquipmentAttributeModifierRule>();
    }
}
