using NiumaAttribute.ViewData;

namespace NiumaAttribute.Service
{
    /// <summary>
    /// 属性查询接口。
    /// 所有查询都显式传入 ActorId，为多角色和后期联机做准备。
    /// </summary>
    public interface IAttributeQuery
    {
        long Revision { get; }

        bool HasActor(string actorId);
        bool HasAttribute(string actorId, string attributeId);
        bool HasResource(string actorId, string resourceId);

        bool TryGetFinalValue(string actorId, string attributeId, out float value);
        float GetFinalValue(string actorId, string attributeId, float fallback = 0f);

        bool TryGetBaseValue(string actorId, string attributeId, out float value);
        bool TryGetResource(string actorId, string resourceId, out ResourceValueViewData value);

        float GetResourceCurrent(string actorId, string resourceId, float fallback = 0f);
        float GetResourceMax(string actorId, string resourceId, float fallback = 0f);
        float GetResourcePercent(string actorId, string resourceId, float fallback = 0f);

        AttributeValueViewData[] GetAllAttributes(string actorId);
        ResourceValueViewData[] GetAllResources(string actorId);
    }
}
