using System;
using System.Collections.Generic;
using NiumaAttribute.Data;
using NiumaAttribute.Enum;

namespace NiumaAttribute.Service
{
    /// <summary>
    /// 属性修饰器集合工具。
    /// 负责校验、覆盖、移除和 Tick 过期，不负责具体属性计算。
    /// </summary>
    public static class AttributeModifierIndex
    {
        /// <summary>
        /// 校验外部传入的修饰器是否满足协议要求。
        /// </summary>
        public static bool IsValid(AttributeModifier modifier, out string message)
        {
            if (modifier == null)
            {
                message = "修饰器为空。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(modifier.SourceId))
            {
                message = "修饰器 SourceId 为空。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(modifier.ModifierId))
            {
                message = "修饰器 ModifierId 为空。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(modifier.TargetAttributeId))
            {
                message = "修饰器 TargetAttributeId 为空。";
                return false;
            }

            if (modifier.Operation == AttributeModifierOperation.None)
            {
                message = "修饰器 Operation 不能为 None。";
                return false;
            }

            if (modifier.Layer == AttributeModifierLayer.None)
            {
                message = "修饰器 Layer 不能为 None。";
                return false;
            }

            message = null;
            return true;
        }

        /// <summary>
        /// 添加或覆盖同 SourceId + ModifierId 的修饰器。
        /// </summary>
        public static bool AddOrReplace(
            ref AttributeModifier[] modifiers,
            AttributeModifier modifier,
            out bool replaced)
        {
            replaced = false;
            var next = modifier.Clone();
            NormalizeDuration(next);

            var source = modifiers ?? Array.Empty<AttributeModifier>();
            for (var i = 0; i < source.Length; i++)
            {
                var current = source[i];
                if (!IsSameKey(current, next.SourceId, next.ModifierId))
                {
                    continue;
                }

                var copy = CloneArray(source);
                copy[i] = next;
                modifiers = copy;
                replaced = true;
                return true;
            }

            var expanded = new AttributeModifier[source.Length + 1];
            Array.Copy(source, expanded, source.Length);
            expanded[expanded.Length - 1] = next;
            modifiers = expanded;
            return true;
        }

        /// <summary>
        /// 移除指定修饰器。
        /// </summary>
        public static bool Remove(
            ref AttributeModifier[] modifiers,
            string sourceId,
            string modifierId,
            out string targetAttributeId)
        {
            targetAttributeId = null;
            var source = modifiers ?? Array.Empty<AttributeModifier>();
            if (source.Length == 0)
            {
                return false;
            }

            var result = new List<AttributeModifier>(source.Length);
            var removed = false;
            for (var i = 0; i < source.Length; i++)
            {
                var modifier = source[i];
                if (IsSameKey(modifier, sourceId, modifierId))
                {
                    removed = true;
                    targetAttributeId = modifier.TargetAttributeId;
                    continue;
                }

                result.Add(modifier);
            }

            if (!removed)
            {
                return false;
            }

            modifiers = result.Count > 0 ? result.ToArray() : Array.Empty<AttributeModifier>();
            return true;
        }

        /// <summary>
        /// 移除某个来源下的所有修饰器。
        /// </summary>
        public static bool RemoveBySource(
            ref AttributeModifier[] modifiers,
            string sourceId,
            List<string> affectedAttributeIds)
        {
            var source = modifiers ?? Array.Empty<AttributeModifier>();
            if (source.Length == 0)
            {
                return false;
            }

            var result = new List<AttributeModifier>(source.Length);
            var removed = false;
            for (var i = 0; i < source.Length; i++)
            {
                var modifier = source[i];
                if (modifier != null && string.Equals(modifier.SourceId, sourceId, StringComparison.Ordinal))
                {
                    removed = true;
                    AddUnique(affectedAttributeIds, modifier.TargetAttributeId);
                    continue;
                }

                result.Add(modifier);
            }

            if (!removed)
            {
                return false;
            }

            modifiers = result.Count > 0 ? result.ToArray() : Array.Empty<AttributeModifier>();
            return true;
        }

        /// <summary>
        /// 推进带持续时间的修饰器，并移除已过期项。
        /// </summary>
        public static bool Tick(
            ref AttributeModifier[] modifiers,
            float deltaTime,
            List<string> expiredTargetAttributeIds)
        {
            var source = modifiers ?? Array.Empty<AttributeModifier>();
            if (source.Length == 0 || deltaTime <= 0f)
            {
                return false;
            }

            var changed = false;
            var result = new List<AttributeModifier>(source.Length);
            for (var i = 0; i < source.Length; i++)
            {
                var modifier = source[i];
                if (modifier == null)
                {
                    changed = true;
                    continue;
                }

                if (modifier.DurationSeconds > 0f)
                {
                    modifier.RemainingSeconds -= deltaTime;
                    changed = true;
                    if (modifier.RemainingSeconds <= 0f)
                    {
                        AddUnique(expiredTargetAttributeIds, modifier.TargetAttributeId);
                        continue;
                    }
                }

                result.Add(modifier);
            }

            if (!changed)
            {
                return false;
            }

            modifiers = result.Count > 0 ? result.ToArray() : Array.Empty<AttributeModifier>();
            return true;
        }

        private static void NormalizeDuration(AttributeModifier modifier)
        {
            if (modifier.DurationSeconds > 0f && modifier.RemainingSeconds <= 0f)
            {
                modifier.RemainingSeconds = modifier.DurationSeconds;
            }

            if (modifier.DurationSeconds <= 0f)
            {
                modifier.RemainingSeconds = 0f;
            }
        }

        private static bool IsSameKey(AttributeModifier modifier, string sourceId, string modifierId)
        {
            return modifier != null
                   && string.Equals(modifier.SourceId, sourceId, StringComparison.Ordinal)
                   && string.Equals(modifier.ModifierId, modifierId, StringComparison.Ordinal);
        }

        private static AttributeModifier[] CloneArray(AttributeModifier[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<AttributeModifier>();
            }

            var result = new AttributeModifier[source.Length];
            Array.Copy(source, result, source.Length);
            return result;
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
