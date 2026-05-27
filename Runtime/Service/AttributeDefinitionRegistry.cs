using System;
using System.Collections.Generic;
using NiumaAttribute.Config;
using UnityEngine;

namespace NiumaAttribute.Service
{
    /// <summary>
    /// 属性与资源定义注册表。
    /// 只保存静态配置引用，不保存任何角色运行时数值。
    /// </summary>
    public sealed class AttributeDefinitionRegistry
    {
        private readonly Dictionary<string, AttributeDefinition> _attributeById =
            new Dictionary<string, AttributeDefinition>(StringComparer.Ordinal);

        private readonly Dictionary<string, ResourceDefinition> _resourceById =
            new Dictionary<string, ResourceDefinition>(StringComparer.Ordinal);

        private readonly List<AttributeDefinition> _attributes = new List<AttributeDefinition>();
        private readonly List<ResourceDefinition> _resources = new List<ResourceDefinition>();

        /// <summary>
        /// 替换属性定义集合。
        /// 重复 AttributeId 会保留第一次出现的配置，并输出警告。
        /// </summary>
        public void SetAttributeDefinitions(IEnumerable<AttributeDefinition> definitions)
        {
            _attributeById.Clear();
            _attributes.Clear();

            if (definitions == null)
            {
                return;
            }

            foreach (var definition in definitions)
            {
                if (definition == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.AttributeId))
                {
                    Debug.LogWarning("[NiumaAttribute] 存在 AttributeId 为空的属性定义，已跳过。");
                    continue;
                }

                if (_attributeById.ContainsKey(definition.AttributeId))
                {
                    Debug.LogWarning($"[NiumaAttribute] 重复的 AttributeId：{definition.AttributeId}，已保留第一次出现的定义。");
                    continue;
                }

                _attributeById.Add(definition.AttributeId, definition);
                _attributes.Add(definition);
            }
        }

        /// <summary>
        /// 替换资源定义集合。
        /// 重复 ResourceId 会保留第一次出现的配置，并输出警告。
        /// </summary>
        public void SetResourceDefinitions(IEnumerable<ResourceDefinition> definitions)
        {
            _resourceById.Clear();
            _resources.Clear();

            if (definitions == null)
            {
                return;
            }

            foreach (var definition in definitions)
            {
                if (definition == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.ResourceId))
                {
                    Debug.LogWarning("[NiumaAttribute] 存在 ResourceId 为空的资源定义，已跳过。");
                    continue;
                }

                if (_resourceById.ContainsKey(definition.ResourceId))
                {
                    Debug.LogWarning($"[NiumaAttribute] 重复的 ResourceId：{definition.ResourceId}，已保留第一次出现的定义。");
                    continue;
                }

                _resourceById.Add(definition.ResourceId, definition);
                _resources.Add(definition);
            }
        }

        public bool TryGetAttribute(string attributeId, out AttributeDefinition definition)
        {
            definition = null;
            return !string.IsNullOrWhiteSpace(attributeId)
                   && _attributeById.TryGetValue(attributeId, out definition);
        }

        public bool TryGetResource(string resourceId, out ResourceDefinition definition)
        {
            definition = null;
            return !string.IsNullOrWhiteSpace(resourceId)
                   && _resourceById.TryGetValue(resourceId, out definition);
        }

        public AttributeDefinition[] GetAllAttributes()
        {
            return _attributes.ToArray();
        }

        public ResourceDefinition[] GetAllResources()
        {
            return _resources.ToArray();
        }
    }
}
