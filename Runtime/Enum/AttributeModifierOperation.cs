namespace NiumaAttribute.Enum
{
    /// <summary>
    /// 属性修饰器运算类型。
    /// </summary>
    public enum AttributeModifierOperation
    {
        /// <summary>
        /// 未设置。运行时传入该值视为非法。
        /// </summary>
        None = 0,

        /// <summary>
        /// 固定加法，例如攻击 +10。
        /// </summary>
        Add = 1,

        /// <summary>
        /// 百分比加法，例如攻击 +20%。
        /// </summary>
        PercentAdd = 2,

        /// <summary>
        /// 最终倍率，例如最大体力 *1.1。
        /// </summary>
        Multiplier = 3
    }
}
