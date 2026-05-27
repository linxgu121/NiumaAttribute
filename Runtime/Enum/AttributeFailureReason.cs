namespace NiumaAttribute.Enum
{
    /// <summary>
    /// 属性模块操作失败原因。
    /// 调用方应使用枚举判断失败类型，不要匹配 Message 字符串。
    /// </summary>
    public enum AttributeFailureReason
    {
        /// <summary>
        /// 无失败。
        /// </summary>
        None = 0,

        /// <summary>
        /// ActorId 为空或非法。
        /// </summary>
        InvalidActorId = 1,

        /// <summary>
        /// 找不到指定 Actor。
        /// </summary>
        ActorNotFound = 2,

        /// <summary>
        /// AttributeId 为空或非法。
        /// </summary>
        InvalidAttributeId = 3,

        /// <summary>
        /// ResourceId 为空或非法。
        /// </summary>
        InvalidResourceId = 4,

        /// <summary>
        /// 找不到指定属性。
        /// </summary>
        AttributeNotFound = 5,

        /// <summary>
        /// 找不到指定资源。
        /// </summary>
        ResourceNotFound = 6,

        /// <summary>
        /// 配置定义缺失。
        /// </summary>
        DefinitionMissing = 7,

        /// <summary>
        /// 数值参数非法，例如消耗量小于等于 0。
        /// </summary>
        InvalidAmount = 8,

        /// <summary>
        /// 修饰器非法。
        /// </summary>
        InvalidModifier = 9,

        /// <summary>
        /// 派生属性存在循环依赖。
        /// </summary>
        CircularDependency = 10,

        /// <summary>
        /// 服务尚未准备好。
        /// </summary>
        ServiceNotReady = 11,

        /// <summary>
        /// 导入快照失败。
        /// </summary>
        ImportFailed = 12
    }
}
