using NiumaAttribute.Data;
using NiumaAttribute.Result;

namespace NiumaAttribute.Service
{
    /// <summary>
    /// 属性命令接口。
    /// 所有方法都可能修改指定 Actor 的属性、资源或修饰器。
    /// </summary>
    public interface IAttributeCommand
    {
        AttributeOperationResult CreateActor(string actorId, string sourceModule = null);
        AttributeOperationResult RemoveActor(string actorId, string sourceModule = null);

        AttributeOperationResult SetBaseValue(string actorId, string attributeId, float value, string sourceModule = null);

        AttributeOperationResult AddModifier(string actorId, AttributeModifier modifier);
        AttributeOperationResult RemoveModifier(string actorId, string sourceId, string modifierId);
        AttributeOperationResult RemoveModifiersBySource(string actorId, string sourceId);

        AttributeOperationResult ConsumeResource(string actorId, string resourceId, float amount, string sourceModule = null);
        AttributeOperationResult RecoverResource(string actorId, string resourceId, float amount, string sourceModule = null);
        AttributeOperationResult SetResourceCurrent(string actorId, string resourceId, float value, string sourceModule = null);
    }
}
