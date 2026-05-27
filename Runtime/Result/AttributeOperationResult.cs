using NiumaAttribute.Enum;

namespace NiumaAttribute.Result
{
    /// <summary>
    /// 属性模块命令操作结果。
    /// </summary>
    public sealed class AttributeOperationResult
    {
        public bool Succeeded;
        public AttributeFailureReason FailureReason;
        public string Message;
        public string TargetId;
        public long Revision;

        public static AttributeOperationResult Success(string targetId = null, long revision = 0L)
        {
            return new AttributeOperationResult
            {
                Succeeded = true,
                FailureReason = AttributeFailureReason.None,
                TargetId = targetId,
                Revision = revision
            };
        }

        public static AttributeOperationResult Failed(
            AttributeFailureReason reason,
            string message = null,
            string targetId = null,
            long revision = 0L)
        {
            return new AttributeOperationResult
            {
                Succeeded = false,
                FailureReason = reason,
                Message = message,
                TargetId = targetId,
                Revision = revision
            };
        }
    }
}
