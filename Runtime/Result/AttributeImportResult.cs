using System;
using NiumaAttribute.Enum;

namespace NiumaAttribute.Result
{
    /// <summary>
    /// 属性快照导入结果。
    /// </summary>
    public sealed class AttributeImportResult
    {
        public bool Succeeded;
        public AttributeFailureReason FailureReason;
        public string Message;
        public string[] Warnings = Array.Empty<string>();
        public long Revision;

        public static AttributeImportResult Success(string[] warnings = null, long revision = 0L)
        {
            return new AttributeImportResult
            {
                Succeeded = true,
                FailureReason = AttributeFailureReason.None,
                Warnings = warnings ?? Array.Empty<string>(),
                Revision = revision
            };
        }

        public static AttributeImportResult Failed(
            AttributeFailureReason reason,
            string message = null,
            string[] warnings = null,
            long revision = 0L)
        {
            return new AttributeImportResult
            {
                Succeeded = false,
                FailureReason = reason,
                Message = message,
                Warnings = warnings ?? Array.Empty<string>(),
                Revision = revision
            };
        }
    }
}
