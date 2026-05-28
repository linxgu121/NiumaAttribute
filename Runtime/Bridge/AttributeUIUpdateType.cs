namespace NiumaAttribute.Bridge
{
    /// <summary>
    /// 属性 UI 更新类型。
    /// </summary>
    public enum AttributeUIUpdateType
    {
        /// <summary>
        /// 未指定更新类型，仅用于默认值保护。
        /// </summary>
        None = 0,

        /// <summary>
        /// 刷新属性面板数据。
        /// </summary>
        Refresh = 1,

        /// <summary>
        /// 清空属性面板数据。
        /// </summary>
        Cleared = 2
    }
}
