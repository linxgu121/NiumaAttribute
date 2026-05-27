namespace NiumaAttribute.Enum
{
    /// <summary>
    /// 属性修饰器分层。
    /// 分层用于保证装备、天赋、效果、剧情等来源的计算顺序稳定。
    /// </summary>
    public enum AttributeModifierLayer
    {
        /// <summary>
        /// 未设置。运行时传入该值视为非法。
        /// </summary>
        None = 0,

        /// <summary>
        /// 成长层，例如等级、修炼、长期成长。
        /// </summary>
        Growth = 10,

        /// <summary>
        /// 装备层，例如武器、防具、族器、饰品。
        /// </summary>
        Equipment = 20,

        /// <summary>
        /// 天赋层，例如被动能力、特质。
        /// </summary>
        Talent = 30,

        /// <summary>
        /// 效果层，例如 Buff、Debuff、元素反应结果。
        /// </summary>
        Effect = 40,

        /// <summary>
        /// 剧情层，例如祝福、诅咒、地区影响。
        /// </summary>
        Story = 50,

        /// <summary>
        /// 运行时临时层，例如技能或交互产生的短期修饰。
        /// </summary>
        Runtime = 60,

        /// <summary>
        /// 调试层，仅用于开发测试。
        /// </summary>
        Debug = 100
    }
}
