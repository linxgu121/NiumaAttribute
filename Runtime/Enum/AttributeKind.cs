namespace NiumaAttribute.Enum
{
    /// <summary>
    /// 属性类型。
    /// 用于区分基础属性、派生属性和资源上限属性。
    /// </summary>
    public enum AttributeKind
    {
        /// <summary>
        /// 未设置。配置中出现该值视为非法。
        /// </summary>
        None = 0,

        /// <summary>
        /// 基础属性，例如力量、敏捷、体质。
        /// </summary>
        Base = 1,

        /// <summary>
        /// 派生属性，例如攻击、防御、暴击率。
        /// </summary>
        Derived = 2,

        /// <summary>
        /// 资源上限属性，例如最大生命、最大体力。
        /// </summary>
        ResourceMax = 3
    }
}
