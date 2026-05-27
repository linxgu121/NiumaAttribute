using System.Collections.Generic;
using NiumaAttribute.Config;

namespace NiumaAttribute.Service
{
    /// <summary>
    /// 属性模块配置能力接口。
    /// 根控制器和调试工具可依赖该接口热更新定义，普通业务模块不应直接依赖配置能力。
    /// </summary>
    public interface IAttributeConfigurationService
    {
        /// <summary>
        /// 替换属性定义集合。
        /// 已存在 Actor 会补齐新增属性并重算最终值。
        /// </summary>
        void SetAttributeDefinitions(IEnumerable<AttributeDefinition> definitions);

        /// <summary>
        /// 替换资源定义集合。
        /// 已存在 Actor 会补齐新增资源并刷新最大值绑定。
        /// </summary>
        void SetResourceDefinitions(IEnumerable<ResourceDefinition> definitions);
    }
}
