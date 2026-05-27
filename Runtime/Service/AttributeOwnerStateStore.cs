using System;
using System.Collections.Generic;
using NiumaAttribute.Config;
using NiumaAttribute.Data;

namespace NiumaAttribute.Service
{
    /// <summary>
    /// Actor 属性状态仓库。
    /// 只负责容器增删查改，不负责最终值计算。
    /// </summary>
    public sealed class AttributeOwnerStateStore
    {
        private readonly Dictionary<string, AttributeOwnerRuntimeState> _owners =
            new Dictionary<string, AttributeOwnerRuntimeState>(StringComparer.Ordinal);

        /// <summary>
        /// 当前 Actor 数量。
        /// </summary>
        public int Count => _owners.Count;

        public bool TryGetOwner(string actorId, out AttributeOwnerRuntimeState owner)
        {
            owner = null;
            return !string.IsNullOrWhiteSpace(actorId) && _owners.TryGetValue(actorId, out owner);
        }

        /// <summary>
        /// 创建 Actor 初始容器，并按当前配置补齐属性和资源状态。
        /// </summary>
        public AttributeOwnerRuntimeState CreateOwner(
            string actorId,
            AttributeDefinitionRegistry registry,
            List<string> addedResourceIds)
        {
            var owner = new AttributeOwnerRuntimeState
            {
                ActorId = actorId
            };

            RefreshOwnerDefinitions(owner, registry, addedResourceIds);
            _owners[actorId] = owner;
            return owner;
        }

        public bool RemoveOwner(string actorId)
        {
            return !string.IsNullOrWhiteSpace(actorId) && _owners.Remove(actorId);
        }

        public void Clear()
        {
            _owners.Clear();
        }

        public void AddImportedOwner(AttributeOwnerRuntimeState owner)
        {
            if (owner == null || string.IsNullOrWhiteSpace(owner.ActorId))
            {
                return;
            }

            _owners[owner.ActorId] = owner;
        }

        /// <summary>
        /// 将当前所有 Actor 复制到输出列表。
        /// 调用方传入缓存列表，避免每帧 Tick 分配数组。
        /// </summary>
        public void CopyOwnersTo(List<AttributeOwnerRuntimeState> output)
        {
            if (output == null)
            {
                return;
            }

            output.Clear();
            foreach (var pair in _owners)
            {
                output.Add(pair.Value);
            }
        }

        /// <summary>
        /// 按当前配置补齐缺失的属性和资源。
        /// 已存在的资源会刷新 MaxAttributeId，确保配置变更后不继续使用旧绑定。
        /// </summary>
        public bool RefreshOwnerDefinitions(
            AttributeOwnerRuntimeState owner,
            AttributeDefinitionRegistry registry,
            List<string> addedResourceIds)
        {
            if (owner == null || registry == null)
            {
                return false;
            }

            var changed = false;
            var attributes = new List<AttributeRuntimeState>(owner.Attributes ?? Array.Empty<AttributeRuntimeState>());
            var attributeDefinitions = registry.GetAllAttributes();
            for (var i = 0; i < attributeDefinitions.Length; i++)
            {
                var definition = attributeDefinitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.AttributeId))
                {
                    continue;
                }

                if (FindAttribute(attributes, definition.AttributeId) != null)
                {
                    continue;
                }

                attributes.Add(new AttributeRuntimeState
                {
                    AttributeId = definition.AttributeId,
                    BaseValue = definition.DefaultBaseValue,
                    FinalValue = definition.DefaultBaseValue
                });
                changed = true;
            }

            var resources = new List<ResourceRuntimeState>(owner.Resources ?? Array.Empty<ResourceRuntimeState>());
            var resourceDefinitions = registry.GetAllResources();
            for (var i = 0; i < resourceDefinitions.Length; i++)
            {
                var definition = resourceDefinitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.ResourceId))
                {
                    continue;
                }

                var resource = FindResource(resources, definition.ResourceId);
                if (resource != null)
                {
                    if (!string.Equals(resource.MaxAttributeId, definition.MaxAttributeId, StringComparison.Ordinal))
                    {
                        resource.MaxAttributeId = definition.MaxAttributeId;
                        changed = true;
                    }

                    continue;
                }

                resources.Add(new ResourceRuntimeState
                {
                    ResourceId = definition.ResourceId,
                    MaxAttributeId = definition.MaxAttributeId
                });
                AddUnique(addedResourceIds, definition.ResourceId);
                changed = true;
            }

            if (changed)
            {
                owner.Attributes = attributes.ToArray();
                owner.Resources = resources.ToArray();
            }

            return changed;
        }

        public static AttributeOwnerRuntimeState FromSnapshot(AttributeOwnerSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.ActorId))
            {
                return null;
            }

            return new AttributeOwnerRuntimeState
            {
                ActorId = snapshot.ActorId,
                Attributes = BuildAttributeStates(snapshot.Attributes),
                Resources = BuildResourceStates(snapshot.Resources),
                Modifiers = BuildModifiers(snapshot.PersistentModifiers)
            };
        }

        public static AttributeRuntimeState FindAttribute(AttributeOwnerRuntimeState owner, string attributeId)
        {
            if (owner == null)
            {
                return null;
            }

            return FindAttribute(owner.Attributes, attributeId);
        }

        public static ResourceRuntimeState FindResource(AttributeOwnerRuntimeState owner, string resourceId)
        {
            if (owner == null)
            {
                return null;
            }

            return FindResource(owner.Resources, resourceId);
        }

        private static AttributeRuntimeState FindAttribute(List<AttributeRuntimeState> attributes, string attributeId)
        {
            if (attributes == null || string.IsNullOrWhiteSpace(attributeId))
            {
                return null;
            }

            for (var i = 0; i < attributes.Count; i++)
            {
                var state = attributes[i];
                if (state != null && string.Equals(state.AttributeId, attributeId, StringComparison.Ordinal))
                {
                    return state;
                }
            }

            return null;
        }

        private static ResourceRuntimeState FindResource(List<ResourceRuntimeState> resources, string resourceId)
        {
            if (resources == null || string.IsNullOrWhiteSpace(resourceId))
            {
                return null;
            }

            for (var i = 0; i < resources.Count; i++)
            {
                var state = resources[i];
                if (state != null && string.Equals(state.ResourceId, resourceId, StringComparison.Ordinal))
                {
                    return state;
                }
            }

            return null;
        }

        private static AttributeRuntimeState FindAttribute(AttributeRuntimeState[] attributes, string attributeId)
        {
            if (attributes == null || string.IsNullOrWhiteSpace(attributeId))
            {
                return null;
            }

            for (var i = 0; i < attributes.Length; i++)
            {
                var state = attributes[i];
                if (state != null && string.Equals(state.AttributeId, attributeId, StringComparison.Ordinal))
                {
                    return state;
                }
            }

            return null;
        }

        private static ResourceRuntimeState FindResource(ResourceRuntimeState[] resources, string resourceId)
        {
            if (resources == null || string.IsNullOrWhiteSpace(resourceId))
            {
                return null;
            }

            for (var i = 0; i < resources.Length; i++)
            {
                var state = resources[i];
                if (state != null && string.Equals(state.ResourceId, resourceId, StringComparison.Ordinal))
                {
                    return state;
                }
            }

            return null;
        }

        private static AttributeRuntimeState[] BuildAttributeStates(AttributeValueSnapshot[] snapshots)
        {
            if (snapshots == null || snapshots.Length == 0)
            {
                return Array.Empty<AttributeRuntimeState>();
            }

            var result = new List<AttributeRuntimeState>(snapshots.Length);
            for (var i = 0; i < snapshots.Length; i++)
            {
                var state = AttributeRuntimeState.FromSnapshot(snapshots[i]);
                if (state == null || string.IsNullOrWhiteSpace(state.AttributeId))
                {
                    continue;
                }

                if (FindAttribute(result, state.AttributeId) == null)
                {
                    result.Add(state);
                }
            }

            return result.Count > 0 ? result.ToArray() : Array.Empty<AttributeRuntimeState>();
        }

        private static ResourceRuntimeState[] BuildResourceStates(ResourceValueSnapshot[] snapshots)
        {
            if (snapshots == null || snapshots.Length == 0)
            {
                return Array.Empty<ResourceRuntimeState>();
            }

            var result = new List<ResourceRuntimeState>(snapshots.Length);
            for (var i = 0; i < snapshots.Length; i++)
            {
                var state = ResourceRuntimeState.FromSnapshot(snapshots[i]);
                if (state == null || string.IsNullOrWhiteSpace(state.ResourceId))
                {
                    continue;
                }

                if (FindResource(result, state.ResourceId) == null)
                {
                    result.Add(state);
                }
            }

            return result.Count > 0 ? result.ToArray() : Array.Empty<ResourceRuntimeState>();
        }

        private static AttributeModifier[] BuildModifiers(AttributeModifierSnapshot[] snapshots)
        {
            if (snapshots == null || snapshots.Length == 0)
            {
                return Array.Empty<AttributeModifier>();
            }

            var byKey = new Dictionary<string, AttributeModifier>(snapshots.Length, StringComparer.Ordinal);
            for (var i = 0; i < snapshots.Length; i++)
            {
                var modifier = snapshots[i]?.ToModifier();
                if (modifier == null || !AttributeModifierIndex.IsValid(modifier, out _))
                {
                    continue;
                }

                byKey[BuildModifierKey(modifier.SourceId, modifier.ModifierId)] = modifier;
            }

            if (byKey.Count == 0)
            {
                return Array.Empty<AttributeModifier>();
            }

            var result = new List<AttributeModifier>(byKey.Values);
            result.Sort(CompareModifierStableKey);
            return result.ToArray();
        }

        private static string BuildModifierKey(string sourceId, string modifierId)
        {
            return $"{sourceId}\u001F{modifierId}";
        }

        private static int CompareModifierStableKey(AttributeModifier left, AttributeModifier right)
        {
            var sourceCompare = string.Compare(left.SourceId, right.SourceId, StringComparison.Ordinal);
            if (sourceCompare != 0)
            {
                return sourceCompare;
            }

            return string.Compare(left.ModifierId, right.ModifierId, StringComparison.Ordinal);
        }

        private static void AddUnique(List<string> list, string value)
        {
            if (list == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], value, StringComparison.Ordinal))
                {
                    return;
                }
            }

            list.Add(value);
        }
    }
}
