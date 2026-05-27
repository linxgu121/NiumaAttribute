namespace NiumaAttribute.ViewData
{
    /// <summary>
    /// 单个资源 UI 表现数据。
    /// </summary>
    public sealed class ResourceValueViewData
    {
        public string ActorId;
        public string ResourceId;
        public string DisplayName;
        public float CurrentValue;
        public float MaxValue;
        public float Percent;
        public bool IsRecovering;
        public float RecoveryPerSecond;
        public bool IsMissingDefinition;
    }
}
