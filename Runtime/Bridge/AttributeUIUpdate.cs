using NiumaAttribute.ViewData;

namespace NiumaAttribute.Bridge
{
    /// <summary>
    /// 属性 UI 更新数据。
    /// 只承载版本号和面板表现数据，避免 UI 直接读取属性运行时状态。
    /// </summary>
    public readonly struct AttributeUIUpdate
    {
        /// <summary>
        /// 更新类型。
        /// </summary>
        public readonly AttributeUIUpdateType UpdateType;

        /// <summary>
        /// 属性模块全局修订号。
        /// </summary>
        public readonly long Revision;

        /// <summary>
        /// 当前属性面板表现数据。
        /// 当前没有可显示数据时为空。
        /// </summary>
        public readonly AttributePanelViewData PanelData;

        /// <summary>
        /// 上一次属性面板表现数据。
        /// 当 UpdateType 为 Cleared 时，UI 可用它判断被清除前的最终状态。
        /// </summary>
        public readonly AttributePanelViewData PreviousPanelData;

        /// <summary>
        /// 当前是否存在属性面板数据。
        /// </summary>
        public bool HasPanelData => PanelData != null;

        public AttributeUIUpdate(
            AttributeUIUpdateType updateType,
            long revision,
            AttributePanelViewData panelData,
            AttributePanelViewData previousPanelData)
        {
            UpdateType = updateType;
            Revision = revision;
            PanelData = panelData;
            PreviousPanelData = previousPanelData;
        }
    }
}
