namespace NiumaAttribute.Bridge
{
    /// <summary>
    /// 属性 UI 接收接口。
    /// 由具体 UI 组件实现，桥接层只负责把整理好的表现数据交给它，不直接操作按钮、面板或预制体。
    /// </summary>
    public interface IAttributeUIReceiver
    {
        /// <summary>
        /// 应用属性 UI 更新。
        /// update 中已经包含指定 Actor 的属性列表、资源列表和版本号。
        /// </summary>
        void ApplyAttributeUpdate(AttributeUIUpdate update);
    }
}
