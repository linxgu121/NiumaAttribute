using System;
using System.Collections.Generic;
using NiumaAttribute.Controller;
using NiumaAttribute.Data;
using NiumaAttribute.Enum;
using NiumaAttribute.Service;
using NiumaEquipment.Controller;
using NiumaEquipment.Data;
using NiumaEquipment.Enum;
using UnityEngine;

namespace NiumaAttribute.EquipmentBridge
{
    /// <summary>
    /// 装备模块到属性模块的修饰器桥接器。
    /// 它根据当前已穿戴装备生成 AttributeModifier，装备卸下或替换时自动移除旧来源。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EquipmentAttributeModifierBridge : MonoBehaviour, IAttributeModifierSource
    {
        private const string DefaultSourcePrefix = "equipment:";
        private const string SourceModuleName = "NiumaEquipment.AttributeBridge";

        [Header("模块引用")]
        [Tooltip("属性模块根控制器。未绑定时会在场景中自动查找 NiumaAttributeController。")]
        [SerializeField] private NiumaAttributeController attributeController;

        [Tooltip("装备模块根控制器。未绑定时会在场景中自动查找 NiumaEquipmentController。")]
        [SerializeField] private NiumaEquipmentController equipmentController;

        [Tooltip("装备属性加成映射表。用于把 EquipmentId 转换为 Attribute Modifier。")]
        [SerializeField] private EquipmentAttributeModifierProfile modifierProfile;

        [Header("Actor 映射")]
        [Tooltip("写入 Attribute 的 ActorId。玩家建议使用 player；多角色项目请为每个角色配置稳定唯一 ID。")]
        [SerializeField] private string actorId = "player";

        [Tooltip("Attribute 中不存在该 Actor 时，是否由桥接器自动创建。")]
        [SerializeField] private bool createActorIfMissing = true;

        [Tooltip("是否要求装备快照的 CurrentOwnerId 与 ActorId 一致。单机早期可以关闭；多角色或联机项目建议开启。")]
        [SerializeField] private bool requireOwnerIdMatch;

        [Tooltip("CurrentOwnerId 为空时是否允许该装备生效。多角色严格模式下建议关闭。")]
        [SerializeField] private bool allowEmptyOwnerId = true;

        [Header("同步策略")]
        [Tooltip("启用时立即同步一次已穿戴装备属性。")]
        [SerializeField] private bool syncOnEnable = true;

        [Tooltip("LateUpdate 中根据 EquipmentRevision 自动检查装备变化。")]
        [SerializeField] private bool syncInLateUpdate = true;

        [Tooltip("桥接器禁用或销毁时是否移除自己写入的装备属性修饰器。建议开启，避免场景切换后残留加成。")]
        [SerializeField] private bool removeModifiersOnDisable = true;

        [Tooltip("修饰器 SourceId 前缀。最终 SourceId 形如 equipment:inventory_instance_id。")]
        [SerializeField] private string sourceIdPrefix = DefaultSourcePrefix;

        [Header("调试")]
        [Tooltip("缺少配置、Actor、装备规则或属性定义时是否输出警告。正式版本可以关闭以减少日志。")]
        [SerializeField] private bool logWarnings = true;

        private readonly Dictionary<string, string> _sourceSignatures = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _currentSources = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _sourcesToRemove = new List<string>();
        private readonly List<AttributeModifier> _modifierBuffer = new List<AttributeModifier>();
        private readonly HashSet<string> _warnedMissingRuleEquipmentIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _warnedMissingTargetAttributes = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _warnedModifierFailureKeys = new HashSet<string>(StringComparer.Ordinal);

        private int _observedEquipmentRevision = -1;
        private string _appliedActorId;
        private bool _lastRefreshHadFailure;
        private bool _warnedMissingAttributeController;
        private bool _warnedMissingEquipmentController;
        private bool _warnedMissingProfile;
        private bool _warnedMissingActor;

        /// <summary>
        /// IAttributeModifierSource 的来源 ID。
        /// 该桥接器实际写入时会使用每件装备自己的 SourceId，避免多件装备互相覆盖。
        /// </summary>
        public string SourceId => SourceModuleName;

        private void Awake()
        {
            ResolveReferences(false);
        }

        private void OnEnable()
        {
            _observedEquipmentRevision = -1;
            if (syncOnEnable)
            {
                RefreshNow();
            }
        }

        private void LateUpdate()
        {
            if (!syncInLateUpdate || equipmentController == null)
            {
                return;
            }

            var revision = equipmentController.EquipmentRevision;
            if (_observedEquipmentRevision == revision)
            {
                return;
            }

            RefreshNow();
        }

        private void OnDisable()
        {
            if (removeModifiersOnDisable)
            {
                RemoveAllAppliedModifiers();
            }

            _observedEquipmentRevision = -1;
        }

        private void OnDestroy()
        {
            if (removeModifiersOnDisable)
            {
                RemoveAllAppliedModifiers();
            }
        }

        /// <summary>
        /// 外部 UI、调试菜单或模块启动器可显式切换目标 Actor。
        /// 切换前会移除旧 Actor 上由本桥接器写入的装备修饰器。
        /// </summary>
        public void SetActorId(string value)
        {
            if (string.Equals(actorId, value, StringComparison.Ordinal))
            {
                return;
            }

            RemoveAllAppliedModifiers();
            actorId = value;
            _observedEquipmentRevision = -1;
            _appliedActorId = null;
        }

        /// <summary>
        /// 立即根据当前已穿戴装备刷新 Attribute Modifier。
        /// </summary>
        [ContextMenu("NiumaAttribute/同步装备属性修饰器")]
        public void RefreshNow()
        {
            if (!EnsureReady())
            {
                return;
            }

            var query = attributeController.AttributeQuery;
            var command = attributeController.AttributeCommand;
            if (query == null || command == null)
            {
                WarnOnce(ref _warnedMissingAttributeController, "属性服务尚未准备好，无法同步装备修饰器。", this);
                return;
            }

            if (!EnsureActor(query, command))
            {
                return;
            }

            if (!string.Equals(_appliedActorId, actorId, StringComparison.Ordinal))
            {
                RemoveAllAppliedModifiers();
                _appliedActorId = actorId;
            }

            _lastRefreshHadFailure = false;
            _currentSources.Clear();
            var equippedItems = equipmentController.GetEquippedItems();
            for (var i = 0; i < equippedItems.Length; i++)
            {
                ApplyEquipmentModifiers(query, command, equippedItems[i]);
            }

            RemoveSourcesNoLongerEquipped(command);

            // 如果本次因为 Attribute 配置缺失或 Modifier 非法而未能完整写入，保持重试状态。
            // 这样团队在 Play 模式下补齐配置后，不需要额外制造一次装备变化。
            _observedEquipmentRevision = _lastRefreshHadFailure ? -1 : equipmentController.EquipmentRevision;
        }

        /// <summary>
        /// 收集当前已穿戴装备会产生的修饰器。
        /// 该方法不写入 AttributeService，仅用于调试或后续聚合式管线。
        /// </summary>
        public void CollectModifiers(string targetActorId, List<AttributeModifier> output)
        {
            if (output == null || modifierProfile == null || equipmentController == null)
            {
                return;
            }

            var equippedItems = equipmentController.GetEquippedItems();
            for (var i = 0; i < equippedItems.Length; i++)
            {
                var snapshot = equippedItems[i];
                if (!ShouldApplyToActor(snapshot, targetActorId)
                    || !modifierProfile.TryGetModifiers(snapshot.EquipmentId, out var rules))
                {
                    continue;
                }

                AppendModifiers(snapshot, rules, output);
            }
        }

        /// <summary>
        /// 移除本桥接器已经写入 Attribute 的所有装备来源。
        /// </summary>
        [ContextMenu("NiumaAttribute/移除装备属性修饰器")]
        public void RemoveAllAppliedModifiers()
        {
            if (attributeController == null || attributeController.AttributeCommand == null)
            {
                _sourceSignatures.Clear();
                return;
            }

            var targetActorId = string.IsNullOrWhiteSpace(_appliedActorId) ? actorId : _appliedActorId;
            if (string.IsNullOrWhiteSpace(targetActorId))
            {
                _sourceSignatures.Clear();
                return;
            }

            var command = attributeController.AttributeCommand;
            foreach (var pair in _sourceSignatures)
            {
                command.RemoveModifiersBySource(targetActorId, pair.Key);
            }

            _sourceSignatures.Clear();
        }

        private bool EnsureReady()
        {
            ResolveReferences(logWarnings);

            if (attributeController == null)
            {
                WarnOnce(ref _warnedMissingAttributeController, "未找到 NiumaAttributeController，请在 Inspector 中绑定属性模块根控制器。", this);
                return false;
            }

            if (equipmentController == null)
            {
                WarnOnce(ref _warnedMissingEquipmentController, "未找到 NiumaEquipmentController，请在 Inspector 中绑定装备模块根控制器。", this);
                return false;
            }

            if (modifierProfile == null)
            {
                WarnOnce(ref _warnedMissingProfile, "未配置 EquipmentAttributeModifierProfile，无法把装备转换为属性修饰器。", this);
                return false;
            }

            if (string.IsNullOrWhiteSpace(actorId))
            {
                WarnOnce(ref _warnedMissingActor, "ActorId 为空，无法写入装备属性修饰器。", this);
                return false;
            }

            return true;
        }

        private void ResolveReferences(bool warnMissing)
        {
            if (attributeController == null)
            {
                attributeController = FindObjectOfType<NiumaAttributeController>();
            }

            if (equipmentController == null)
            {
                equipmentController = FindObjectOfType<NiumaEquipmentController>();
            }

            if (warnMissing && attributeController == null)
            {
                WarnOnce(ref _warnedMissingAttributeController, "自动查找 NiumaAttributeController 失败，请手动绑定。", this);
            }

            if (warnMissing && equipmentController == null)
            {
                WarnOnce(ref _warnedMissingEquipmentController, "自动查找 NiumaEquipmentController 失败，请手动绑定。", this);
            }
        }

        private bool EnsureActor(IAttributeQuery query, IAttributeCommand command)
        {
            if (query.HasActor(actorId))
            {
                return true;
            }

            if (!createActorIfMissing)
            {
                WarnOnce(ref _warnedMissingActor, $"Attribute 中不存在 Actor：{actorId}，且未开启自动创建。", this);
                return false;
            }

            var result = command.CreateActor(actorId, SourceModuleName);
            if (result == null || !result.Succeeded)
            {
                WarnOnce(ref _warnedMissingActor, $"创建 Attribute Actor 失败：ActorId={actorId}, Reason={result?.FailureReason}, Message={result?.Message}", this);
                return false;
            }

            return true;
        }

        private void ApplyEquipmentModifiers(IAttributeQuery query, IAttributeCommand command, EquipmentInstanceSnapshot snapshot)
        {
            if (!ShouldApplyToActor(snapshot, actorId))
            {
                return;
            }

            if (!modifierProfile.TryGetModifiers(snapshot.EquipmentId, out var rules))
            {
                WarnMissingRule(snapshot.EquipmentId);
                return;
            }

            var sourceId = BuildSourceId(snapshot);
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                return;
            }

            _currentSources.Add(sourceId);

            var signature = BuildSignature(snapshot, rules);
            if (_sourceSignatures.TryGetValue(sourceId, out var oldSignature)
                && string.Equals(oldSignature, signature, StringComparison.Ordinal))
            {
                return;
            }

            if (_sourceSignatures.ContainsKey(sourceId))
            {
                command.RemoveModifiersBySource(actorId, sourceId);
            }

            _modifierBuffer.Clear();
            AppendModifiers(snapshot, rules, _modifierBuffer);
            var appliedCount = 0;
            for (var i = 0; i < _modifierBuffer.Count; i++)
            {
                var modifier = _modifierBuffer[i];
                if (!query.HasAttribute(actorId, modifier.TargetAttributeId))
                {
                    _lastRefreshHadFailure = true;
                    WarnMissingTargetAttribute(snapshot.EquipmentId, modifier.TargetAttributeId);
                    continue;
                }

                var result = command.AddModifier(actorId, modifier);
                if (result == null || !result.Succeeded)
                {
                    _lastRefreshHadFailure = true;
                    WarnModifierFailure(snapshot.EquipmentId, modifier.ModifierId, result?.FailureReason.ToString(), result?.Message);
                    continue;
                }

                appliedCount++;
            }

            if (appliedCount > 0 && !_lastRefreshHadFailure)
            {
                _sourceSignatures[sourceId] = signature;
            }
        }

        private void RemoveSourcesNoLongerEquipped(IAttributeCommand command)
        {
            _sourcesToRemove.Clear();
            foreach (var pair in _sourceSignatures)
            {
                if (!_currentSources.Contains(pair.Key))
                {
                    _sourcesToRemove.Add(pair.Key);
                }
            }

            for (var i = 0; i < _sourcesToRemove.Count; i++)
            {
                var sourceId = _sourcesToRemove[i];
                command.RemoveModifiersBySource(actorId, sourceId);
                _sourceSignatures.Remove(sourceId);
            }
        }

        private bool ShouldApplyToActor(EquipmentInstanceSnapshot snapshot, string targetActorId)
        {
            if (snapshot == null || snapshot.State != EquipmentState.Equipped)
            {
                return false;
            }

            if (!requireOwnerIdMatch)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(snapshot.CurrentOwnerId))
            {
                return allowEmptyOwnerId;
            }

            return string.Equals(snapshot.CurrentOwnerId, targetActorId, StringComparison.Ordinal);
        }

        private void AppendModifiers(
            EquipmentInstanceSnapshot snapshot,
            EquipmentAttributeModifierRule[] rules,
            List<AttributeModifier> output)
        {
            if (snapshot == null || rules == null || output == null)
            {
                return;
            }

            var sourceId = BuildSourceId(snapshot);
            for (var i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                if (rule == null || string.IsNullOrWhiteSpace(rule.TargetAttributeId))
                {
                    continue;
                }

                output.Add(new AttributeModifier
                {
                    SourceId = sourceId,
                    ModifierId = BuildModifierId(snapshot, rule, i),
                    TargetAttributeId = rule.TargetAttributeId,
                    Operation = rule.Operation,
                    Layer = rule.Layer == AttributeModifierLayer.None ? AttributeModifierLayer.Equipment : rule.Layer,
                    Value = rule.Value,
                    Priority = rule.Priority,
                    IsPersistent = false,
                    DurationSeconds = 0f,
                    RemainingSeconds = 0f,
                    SourceModule = SourceModuleName
                });
            }
        }

        private string BuildSourceId(EquipmentInstanceSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.InventoryInstanceId))
            {
                return null;
            }

            var prefix = string.IsNullOrWhiteSpace(sourceIdPrefix) ? DefaultSourcePrefix : sourceIdPrefix;
            return prefix + snapshot.InventoryInstanceId;
        }

        private static string BuildModifierId(EquipmentInstanceSnapshot snapshot, EquipmentAttributeModifierRule rule, int index)
        {
            if (rule != null && !string.IsNullOrWhiteSpace(rule.ModifierId))
            {
                return rule.ModifierId;
            }

            var equipmentId = snapshot != null && !string.IsNullOrWhiteSpace(snapshot.EquipmentId)
                ? snapshot.EquipmentId
                : "unknown";
            var target = rule != null && !string.IsNullOrWhiteSpace(rule.TargetAttributeId)
                ? rule.TargetAttributeId
                : "attribute";
            return $"{equipmentId}:{target}:{index}";
        }

        private static string BuildSignature(EquipmentInstanceSnapshot snapshot, EquipmentAttributeModifierRule[] rules)
        {
            var signature = snapshot != null ? snapshot.EquipmentId : string.Empty;
            if (rules == null || rules.Length == 0)
            {
                return signature;
            }

            for (var i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                if (rule == null)
                {
                    continue;
                }

                signature += $"|{BuildModifierId(snapshot, rule, i)}:{rule.TargetAttributeId}:{(int)rule.Operation}:{(int)rule.Layer}:{rule.Value}:{rule.Priority}";
            }

            return signature;
        }

        private void WarnMissingRule(string equipmentId)
        {
            if (!logWarnings || string.IsNullOrWhiteSpace(equipmentId) || _warnedMissingRuleEquipmentIds.Contains(equipmentId))
            {
                return;
            }

            Debug.LogWarning($"[EquipmentAttributeModifierBridge] 未找到装备属性加成配置：EquipmentId={equipmentId}。如果该装备不提供属性加成，可以忽略。", this);
            _warnedMissingRuleEquipmentIds.Add(equipmentId);
        }

        private void WarnMissingTargetAttribute(string equipmentId, string attributeId)
        {
            if (!logWarnings)
            {
                return;
            }

            var key = $"{equipmentId}:{attributeId}";
            if (_warnedMissingTargetAttributes.Contains(key))
            {
                return;
            }

            Debug.LogWarning($"[EquipmentAttributeModifierBridge] Attribute 中缺少目标属性：Actor={actorId}, AttributeId={attributeId}, EquipmentId={equipmentId}", this);
            _warnedMissingTargetAttributes.Add(key);
        }

        private void WarnModifierFailure(string equipmentId, string modifierId, string reason, string message)
        {
            if (!logWarnings)
            {
                return;
            }

            var key = $"{equipmentId}:{modifierId}:{reason}";
            if (_warnedModifierFailureKeys.Contains(key))
            {
                return;
            }

            Debug.LogWarning($"[EquipmentAttributeModifierBridge] 添加装备属性修饰器失败：EquipmentId={equipmentId}, ModifierId={modifierId}, Reason={reason}, Message={message}", this);
            _warnedModifierFailureKeys.Add(key);
        }

        private void WarnOnce(ref bool flag, string message, UnityEngine.Object context)
        {
            if (!logWarnings || flag)
            {
                return;
            }

            Debug.LogWarning($"[EquipmentAttributeModifierBridge] {message}", context);
            flag = true;
        }
    }
}
