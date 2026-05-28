using System;
using System.Collections.Generic;
using NiumaAttribute.Config;
using NiumaAttribute.Data;
using NiumaAttribute.Enum;

namespace NiumaAttribute.Service
{
    /// <summary>
    /// 资源运行时更新器。
    /// 负责最大值缓存、当前值 Clamp、初始化和基础自动恢复。
    /// </summary>
    public sealed class ResourceRuntimeUpdater
    {
        /// <summary>
        /// 属性重算后刷新资源最大值。
        /// 对新增资源执行初始化；对已有资源按百分比保留当前值。
        /// </summary>
        public bool RefreshMaxValues(
            AttributeOwnerRuntimeState owner,
            AttributeDefinitionRegistry registry,
            IReadOnlyList<string> initializeResourceIds,
            bool preservePercent)
        {
            if (owner == null)
            {
                return false;
            }

            var changed = false;
            var resources = owner.Resources ?? Array.Empty<ResourceRuntimeState>();
            for (var i = 0; i < resources.Length; i++)
            {
                var resource = resources[i];
                if (resource == null || string.IsNullOrWhiteSpace(resource.ResourceId))
                {
                    continue;
                }

                var definitionExists = registry.TryGetResource(resource.ResourceId, out var definition);
                if (definitionExists)
                {
                    resource.MaxAttributeId = definition.MaxAttributeId;
                }

                var oldMax = resource.MaxValueCache;
                var newMax = GetMaxValue(owner, resource.MaxAttributeId);
                var shouldInitialize = Contains(initializeResourceIds, resource.ResourceId);
                if (shouldInitialize && definitionExists)
                {
                    resource.MaxValueCache = newMax;
                    resource.CurrentValue = BuildInitialValue(definition, newMax);
                    resource.RecoveryDelayTimer = 0f;
                    changed = true;
                    continue;
                }

                resource.MaxValueCache = newMax;
                var nextCurrent = resource.CurrentValue;
                if (preservePercent && oldMax > 0f && Math.Abs(oldMax - newMax) > 0.0001f)
                {
                    nextCurrent = resource.CurrentValue / oldMax * newMax;
                }

                nextCurrent = ClampCurrent(nextCurrent, newMax, false);
                if (Math.Abs(nextCurrent - resource.CurrentValue) > 0.0001f
                    || Math.Abs(oldMax - newMax) > 0.0001f)
                {
                    resource.CurrentValue = nextCurrent;
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>
        /// 处理资源基础自动恢复。
        /// </summary>
        public bool TickAutoRecovery(
            AttributeOwnerRuntimeState owner,
            AttributeDefinitionRegistry registry,
            float deltaTime)
        {
            if (owner == null || deltaTime <= 0f)
            {
                return false;
            }

            var changed = false;
            var resources = owner.Resources ?? Array.Empty<ResourceRuntimeState>();
            for (var i = 0; i < resources.Length; i++)
            {
                var resource = resources[i];
                if (resource == null
                    || !registry.TryGetResource(resource.ResourceId, out var definition)
                    || !definition.EnableAutoRecovery
                    || definition.RecoveryPerSecond <= 0f)
                {
                    continue;
                }

                if (resource.RecoveryDelayTimer > 0f)
                {
                    var nextDelay = Math.Max(0f, resource.RecoveryDelayTimer - deltaTime);
                    if (Math.Abs(nextDelay - resource.RecoveryDelayTimer) > 0.0001f)
                    {
                        resource.RecoveryDelayTimer = nextDelay;
                    }

                    if (resource.RecoveryDelayTimer > 0f)
                    {
                        continue;
                    }
                }

                var maxValue = resource.MaxValueCache;
                if (!definition.AllowOverRecover && resource.CurrentValue >= maxValue)
                {
                    continue;
                }

                var nextCurrent = resource.CurrentValue + definition.RecoveryPerSecond * deltaTime;
                nextCurrent = ClampCurrent(nextCurrent, maxValue, definition.AllowOverRecover);
                if (Math.Abs(nextCurrent - resource.CurrentValue) > 0.0001f)
                {
                    resource.CurrentValue = nextCurrent;
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>
        /// 限制资源当前值。
        /// 当 maxValue 小于等于 0 且不允许超量恢复时，当前值会被压到 0。
        /// </summary>
        public static float ClampCurrent(float value, float maxValue, bool allowOverRecover)
        {
            if (value < 0f)
            {
                return 0f;
            }

            if (!allowOverRecover && value > maxValue)
            {
                return Math.Max(0f, maxValue);
            }

            return value;
        }

        private static float BuildInitialValue(ResourceDefinition definition, float maxValue)
        {
            switch (definition.InitialMode)
            {
                case ResourceInitialMode.Zero:
                    return 0f;
                case ResourceInitialMode.FixedValue:
                    return ClampCurrent(definition.InitialValue, maxValue, definition.AllowOverRecover);
                case ResourceInitialMode.Percent:
                    return ClampCurrent(maxValue * definition.InitialValue, maxValue, definition.AllowOverRecover);
                default:
                    return ClampCurrent(maxValue, maxValue, definition.AllowOverRecover);
            }
        }

        private static float GetMaxValue(AttributeOwnerRuntimeState owner, string maxAttributeId)
        {
            if (owner == null || string.IsNullOrWhiteSpace(maxAttributeId))
            {
                return 0f;
            }

            var state = AttributeOwnerStateStore.FindAttribute(owner, maxAttributeId);
            return state != null ? state.FinalValue : 0f;
        }

        private static bool Contains(IReadOnlyList<string> values, string target)
        {
            if (values == null || string.IsNullOrWhiteSpace(target))
            {
                return false;
            }

            for (var i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], target, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
