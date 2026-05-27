using System;
using NiumaAttribute.Enum;
using UnityEngine;

namespace NiumaAttribute.Config
{
    /// <summary>
    /// 属性定义。
    /// 静态配置由策划维护，不保存角色运行时进度。
    /// </summary>
    [CreateAssetMenu(menuName = "NiumaAttribute/Attribute Definition", fileName = "AttributeDefinition")]
    public sealed class AttributeDefinition : ScriptableObject
    {
        [Tooltip("属性稳定 ID。用于存档、跨模块引用和调试，不要随意改名。")]
        public string AttributeId;

        [Tooltip("属性显示名称。")]
        public string DisplayName;

        [Tooltip("属性说明。")]
        [TextArea]
        public string Description;

        [Tooltip("属性类型：基础属性、派生属性、资源上限属性。None 只用于默认值保护。")]
        public AttributeKind Kind = AttributeKind.None;

        [Tooltip("默认基础值。角色创建或配置新增时使用。")]
        public float DefaultBaseValue;

        [Tooltip("是否启用最小值限制。")]
        public bool UseMinValue;

        [Tooltip("最小值。仅在 UseMinValue 开启时生效。")]
        public float MinValue;

        [Tooltip("是否启用最大值限制。")]
        public bool UseMaxValue;

        [Tooltip("最大值。仅在 UseMaxValue 开启时生效。")]
        public float MaxValue;

        [Tooltip("派生属性计算项。基础属性通常保持为空。")]
        public DerivedAttributeTermData[] DerivedTerms = Array.Empty<DerivedAttributeTermData>();

        [Tooltip("分类标签。用于 UI、调试和后续条件查询。建议统一小写。")]
        public string[] Tags = Array.Empty<string>();
    }
}
