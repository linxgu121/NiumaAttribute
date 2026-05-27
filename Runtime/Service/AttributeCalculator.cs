using System;
using System.Collections.Generic;
using NiumaAttribute.Config;
using NiumaAttribute.Data;
using NiumaAttribute.Enum;
using NiumaAttribute.ViewData;

namespace NiumaAttribute.Service
{
    /// <summary>
    /// 属性最终值计算器。
    /// 负责派生公式、分层修饰器、Clamp 和循环依赖保护。
    /// </summary>
    public sealed class AttributeCalculator
    {
        private readonly AttributeDefinitionRegistry _registry;
        private readonly List<AttributeModifier> _modifierBuffer = new List<AttributeModifier>();

        public AttributeCalculator(AttributeDefinitionRegistry registry)
        {
            _registry = registry;
        }

        /// <summary>
        /// 重算指定 Actor 的所有属性最终值。
        /// </summary>
        public bool Recalculate(AttributeOwnerRuntimeState owner, out string error)
        {
            error = null;
            if (owner == null)
            {
                error = "Actor 属性容器为空。";
                return false;
            }

            var resolved = new Dictionary<string, float>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var attributes = owner.Attributes ?? Array.Empty<AttributeRuntimeState>();
            for (var i = 0; i < attributes.Length; i++)
            {
                var state = attributes[i];
                if (state == null || string.IsNullOrWhiteSpace(state.AttributeId))
                {
                    continue;
                }

                if (!TryComputeValue(owner, state.AttributeId, resolved, visiting, out var value, out error))
                {
                    return false;
                }

                state.FinalValue = value;
            }

            return true;
        }

        /// <summary>
        /// 构建指定属性的分层贡献数据，用于 UI 和调试面板。
        /// </summary>
        public AttributeLayerValueViewData[] BuildLayerViewData(AttributeOwnerRuntimeState owner, string attributeId)
        {
            if (owner == null || string.IsNullOrWhiteSpace(attributeId))
            {
                return Array.Empty<AttributeLayerValueViewData>();
            }

            var modifiers = owner.Modifiers ?? Array.Empty<AttributeModifier>();
            if (modifiers.Length == 0)
            {
                return Array.Empty<AttributeLayerValueViewData>();
            }

            var layers = new Dictionary<AttributeModifierLayer, AttributeLayerValueViewData>();
            for (var i = 0; i < modifiers.Length; i++)
            {
                var modifier = modifiers[i];
                if (modifier == null
                    || !string.Equals(modifier.TargetAttributeId, attributeId, StringComparison.Ordinal)
                    || modifier.Layer == AttributeModifierLayer.None)
                {
                    continue;
                }

                if (!layers.TryGetValue(modifier.Layer, out var layerData))
                {
                    layerData = new AttributeLayerValueViewData
                    {
                        Layer = modifier.Layer,
                        MultiplierValue = 1f
                    };
                    layers.Add(modifier.Layer, layerData);
                }

                switch (modifier.Operation)
                {
                    case AttributeModifierOperation.Add:
                        layerData.AddValue += modifier.Value;
                        break;
                    case AttributeModifierOperation.PercentAdd:
                        layerData.PercentAddValue += modifier.Value;
                        break;
                    case AttributeModifierOperation.Multiplier:
                        layerData.MultiplierValue *= modifier.Value;
                        break;
                }
            }

            if (layers.Count == 0)
            {
                return Array.Empty<AttributeLayerValueViewData>();
            }

            var result = new List<AttributeLayerValueViewData>(layers.Values);
            result.Sort((left, right) => left.Layer.CompareTo(right.Layer));
            return result.ToArray();
        }

        /// <summary>
        /// 查找影响指定属性的主要修饰层。
        /// 当前按绝对贡献值估算，便于 UI 快速显示来源。
        /// </summary>
        public AttributeModifierLayer FindDominantLayer(AttributeOwnerRuntimeState owner, string attributeId)
        {
            var layers = BuildLayerViewData(owner, attributeId);
            var dominant = AttributeModifierLayer.None;
            var bestScore = 0f;
            for (var i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                var score = Math.Abs(layer.AddValue)
                            + Math.Abs(layer.PercentAddValue)
                            + Math.Abs(layer.MultiplierValue - 1f);
                if (score > bestScore)
                {
                    bestScore = score;
                    dominant = layer.Layer;
                }
            }

            return dominant;
        }

        private bool TryComputeValue(
            AttributeOwnerRuntimeState owner,
            string attributeId,
            Dictionary<string, float> resolved,
            HashSet<string> visiting,
            out float value,
            out string error)
        {
            value = 0f;
            error = null;

            if (resolved.TryGetValue(attributeId, out value))
            {
                return true;
            }

            if (visiting.Contains(attributeId))
            {
                error = $"属性派生公式存在循环依赖：{attributeId}";
                return false;
            }

            var state = AttributeOwnerStateStore.FindAttribute(owner, attributeId);
            if (state == null)
            {
                if (_registry.TryGetAttribute(attributeId, out var missingStateDefinition))
                {
                    value = missingStateDefinition.DefaultBaseValue;
                    resolved[attributeId] = value;
                    return true;
                }

                value = 0f;
                resolved[attributeId] = value;
                return true;
            }

            visiting.Add(attributeId);
            var rawValue = state.BaseValue;
            if (_registry.TryGetAttribute(attributeId, out var definition))
            {
                rawValue = CalculateFormulaValue(owner, definition, state, resolved, visiting, out error);
                if (error != null)
                {
                    visiting.Remove(attributeId);
                    return false;
                }
            }

            value = ApplyModifiers(rawValue, owner.Modifiers, attributeId);
            if (definition != null)
            {
                value = ClampValue(value, definition);
            }

            visiting.Remove(attributeId);
            resolved[attributeId] = value;
            return true;
        }

        private float CalculateFormulaValue(
            AttributeOwnerRuntimeState owner,
            AttributeDefinition definition,
            AttributeRuntimeState state,
            Dictionary<string, float> resolved,
            HashSet<string> visiting,
            out string error)
        {
            error = null;
            var value = state.BaseValue;
            var terms = definition.DerivedTerms ?? Array.Empty<DerivedAttributeTermData>();
            for (var i = 0; i < terms.Length; i++)
            {
                var term = terms[i];
                if (term == null || string.IsNullOrWhiteSpace(term.SourceAttributeId))
                {
                    continue;
                }

                if (!TryComputeValue(owner, term.SourceAttributeId, resolved, visiting, out var sourceValue, out error))
                {
                    return value;
                }

                value += sourceValue * term.Coefficient;
            }

            return value;
        }

        private float ApplyModifiers(float baseValue, AttributeModifier[] modifiers, string attributeId)
        {
            _modifierBuffer.Clear();
            if (modifiers != null)
            {
                for (var i = 0; i < modifiers.Length; i++)
                {
                    var modifier = modifiers[i];
                    if (modifier != null && string.Equals(modifier.TargetAttributeId, attributeId, StringComparison.Ordinal))
                    {
                        _modifierBuffer.Add(modifier);
                    }
                }
            }

            if (_modifierBuffer.Count == 0)
            {
                return baseValue;
            }

            // TODO: 如果单个 Actor 的 Modifier 数量明显增多，可在 Modifier 写入阶段维护排序缓存，减少重算时排序开销。
            _modifierBuffer.Sort(CompareModifier);
            var value = baseValue;
            var currentLayer = AttributeModifierLayer.None;
            var add = 0f;
            var percent = 0f;
            var multiplier = 1f;
            var hasLayer = false;

            for (var i = 0; i < _modifierBuffer.Count; i++)
            {
                var modifier = _modifierBuffer[i];
                if (!hasLayer)
                {
                    currentLayer = modifier.Layer;
                    hasLayer = true;
                }
                else if (modifier.Layer != currentLayer)
                {
                    value = ApplyLayer(value, add, percent, multiplier);
                    currentLayer = modifier.Layer;
                    add = 0f;
                    percent = 0f;
                    multiplier = 1f;
                }

                switch (modifier.Operation)
                {
                    case AttributeModifierOperation.Add:
                        add += modifier.Value;
                        break;
                    case AttributeModifierOperation.PercentAdd:
                        percent += modifier.Value;
                        break;
                    case AttributeModifierOperation.Multiplier:
                        multiplier *= modifier.Value;
                        break;
                }
            }

            return ApplyLayer(value, add, percent, multiplier);
        }

        private static float ApplyLayer(float value, float add, float percent, float multiplier)
        {
            return (value + add) * (1f + percent) * multiplier;
        }

        private static int CompareModifier(AttributeModifier left, AttributeModifier right)
        {
            var layerCompare = left.Layer.CompareTo(right.Layer);
            if (layerCompare != 0)
            {
                return layerCompare;
            }

            var operationCompare = left.Operation.CompareTo(right.Operation);
            if (operationCompare != 0)
            {
                return operationCompare;
            }

            var priorityCompare = left.Priority.CompareTo(right.Priority);
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            var sourceCompare = string.Compare(left.SourceId, right.SourceId, StringComparison.Ordinal);
            if (sourceCompare != 0)
            {
                return sourceCompare;
            }

            return string.Compare(left.ModifierId, right.ModifierId, StringComparison.Ordinal);
        }

        private static float ClampValue(float value, AttributeDefinition definition)
        {
            if (definition.UseMinValue && value < definition.MinValue)
            {
                value = definition.MinValue;
            }

            if (definition.UseMaxValue && value > definition.MaxValue)
            {
                value = definition.MaxValue;
            }

            return value;
        }
    }
}
