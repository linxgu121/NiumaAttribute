namespace NiumaAttribute.Enum
{
    /// <summary>
    /// 资源初始值模式。
    /// </summary>
    public enum ResourceInitialMode
    {
        /// <summary>
        /// 初始为最大值。
        /// </summary>
        Full = 0,

        /// <summary>
        /// 初始为 0。
        /// </summary>
        Zero = 1,

        /// <summary>
        /// 使用固定初始值。
        /// </summary>
        FixedValue = 2,

        /// <summary>
        /// 使用最大值百分比初始化。
        /// </summary>
        Percent = 3
    }
}
