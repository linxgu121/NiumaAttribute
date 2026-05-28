using NiumaAttribute.ViewData;

namespace NiumaAttribute.Bridge
{
    /// <summary>
    /// 单个资源条 UI 更新数据。
    /// 用于 HP、MP、Stamina 等高频显示，不重建完整属性面板。
    /// </summary>
    public readonly struct AttributeResourceBarUpdate
    {
        /// <summary>
        /// 属性模块全局修订号。
        /// </summary>
        public readonly long Revision;

        /// <summary>
        /// 当前资源表现数据。资源不存在或服务未就绪时为空。
        /// </summary>
        public readonly ResourceValueViewData ResourceData;

        /// <summary>
        /// 上一次资源表现数据。
        /// UI 可用它做数值缓动。
        /// </summary>
        public readonly ResourceValueViewData PreviousResourceData;

        /// <summary>
        /// 当前是否存在资源数据。
        /// </summary>
        public bool HasResourceData => ResourceData != null;

        public AttributeResourceBarUpdate(
            long revision,
            ResourceValueViewData resourceData,
            ResourceValueViewData previousResourceData)
        {
            Revision = revision;
            ResourceData = resourceData;
            PreviousResourceData = previousResourceData;
        }
    }
}
