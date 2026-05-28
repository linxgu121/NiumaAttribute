using System;
using NiumaAttribute.Enum;
using UnityEngine;

namespace NiumaAttribute.EquipmentBridge
{
    /// <summary>
    /// 单条装备属性加成规则。
    /// 该数据只描述“穿上某件装备后给哪个 Attribute 增加什么 Modifier”，不保存运行时状态。
    /// </summary>
    [Serializable]
    public sealed class EquipmentAttributeModifierRule
    {
        [Tooltip("规则 ID。同一件装备内必须稳定唯一；为空时桥接器会用序号兜底，但正式内容建议填写。")]
        public string ModifierId;

        [Tooltip("目标属性 ID，需要与 NiumaAttribute 的 AttributeDefinition.AttributeId 保持一致。")]
        public string TargetAttributeId;

        [Tooltip("修饰器运算类型。装备常用 Add 或 PercentAdd，Multiplier 请谨慎使用。")]
        public AttributeModifierOperation Operation = AttributeModifierOperation.Add;

        [Tooltip("修饰器分层。装备加成通常使用 Equipment，不建议改成 Effect 或 Story。")]
        public AttributeModifierLayer Layer = AttributeModifierLayer.Equipment;

        [Tooltip("修饰器数值。例如攻击 +10 填 10；攻击 +20% 使用 PercentAdd 并填 0.2。")]
        public float Value;

        [Tooltip("同层同运算下的排序优先级。数值越小越先计算。")]
        public int Priority;
    }
}
