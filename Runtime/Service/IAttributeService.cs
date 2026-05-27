using NiumaAttribute.Data;
using NiumaAttribute.Result;

namespace NiumaAttribute.Service
{
    /// <summary>
    /// 属性服务门面。
    /// 第一版聚合查询、命令、Tick 和快照能力，后续接口膨胀时再拆分能力接口。
    /// </summary>
    public interface IAttributeService : IAttributeQuery, IAttributeCommand
    {
        AttributeSaveData ExportSnapshot();
        AttributeImportResult ImportSnapshot(AttributeSaveData snapshot);
        void Tick(float deltaTime);
        void RecalculateActor(string actorId);
        void RecalculateAll();
    }
}
