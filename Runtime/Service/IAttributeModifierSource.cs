using System.Collections.Generic;
using NiumaAttribute.Data;

namespace NiumaAttribute.Service
{
    /// <summary>
    /// 属性修饰器来源接口。
    /// 装备、效果、剧情等模块可以实现该接口，为指定 Actor 批量输出修饰器。
    /// </summary>
    public interface IAttributeModifierSource
    {
        string SourceId { get; }
        void CollectModifiers(string actorId, List<AttributeModifier> output);
    }
}
