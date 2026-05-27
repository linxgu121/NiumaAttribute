namespace NiumaAttribute.Enum
{
    /// <summary>
    /// 资源类型。
    /// 该枚举只用于分类和表现，不限制资源 ID 扩展。
    /// </summary>
    public enum ResourceKind
    {
        /// <summary>
        /// 未设置。配置中出现该值视为非法。
        /// </summary>
        None = 0,

        /// <summary>
        /// 常规可消耗资源，例如生命、法力、体力。
        /// </summary>
        Consumable = 1,

        /// <summary>
        /// 技能或战斗能量，例如怒气、专注。
        /// </summary>
        Energy = 2,

        /// <summary>
        /// 项目自定义资源，例如士气、氏族影响力。
        /// </summary>
        Special = 3
    }
}
