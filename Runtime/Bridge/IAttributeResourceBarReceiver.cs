namespace NiumaAttribute.Bridge
{
    /// <summary>
    /// 属性资源条 UI 接收接口。
    /// 一个接收器通常对应一个 HP、MP 或 Stamina 条。
    /// </summary>
    public interface IAttributeResourceBarReceiver
    {
        /// <summary>
        /// 应用资源条更新。
        /// </summary>
        void ApplyResourceBarUpdate(AttributeResourceBarUpdate update);
    }
}
