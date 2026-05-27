using System;

namespace NiumaAttribute.ViewData
{
    /// <summary>
    /// 属性面板表现数据。
    /// UI 桥接层按 ActorId 输出该数据。
    /// </summary>
    public sealed class AttributePanelViewData
    {
        public string ActorId;
        public long Revision;
        public AttributeValueViewData[] Attributes = Array.Empty<AttributeValueViewData>();
        public ResourceValueViewData[] Resources = Array.Empty<ResourceValueViewData>();
    }
}
