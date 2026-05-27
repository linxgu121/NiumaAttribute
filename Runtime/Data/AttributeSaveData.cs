using System;

namespace NiumaAttribute.Data
{
    /// <summary>
    /// 属性模块存档根数据。
    /// 一个 Section 可以保存多个 Actor 的属性和资源快照。
    /// </summary>
    [Serializable]
    public sealed class AttributeSaveData
    {
        public int Version = 1;
        public long Revision;
        public AttributeOwnerSnapshot[] Owners = Array.Empty<AttributeOwnerSnapshot>();
    }
}
