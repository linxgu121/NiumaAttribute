using System;
using System.Collections.Generic;
using NiumaAttribute.Config;
using NiumaAttribute.Data;
using NiumaAttribute.Enum;
using NiumaAttribute.Result;
using NiumaAttribute.ViewData;
using UnityEngine;

namespace NiumaAttribute.Service
{
    /// <summary>
    /// 属性核心服务。
    /// 负责 Actor 数值容器、属性计算、资源修改、修饰器生命周期和快照导入导出。
    /// </summary>
    public sealed class AttributeService : IAttributeService, IAttributeConfigurationService
    {
        private const int CurrentSaveVersion = 1;

        private readonly AttributeDefinitionRegistry _definitionRegistry = new AttributeDefinitionRegistry();
        private readonly AttributeCalculator _calculator;
        private readonly ResourceRuntimeUpdater _resourceUpdater = new ResourceRuntimeUpdater();
        private readonly List<AttributeOwnerRuntimeState> _ownerBuffer = new List<AttributeOwnerRuntimeState>();
        private readonly List<string> _resourceIdBuffer = new List<string>();
        private readonly List<string> _affectedAttributeBuffer = new List<string>();

        private AttributeOwnerStateStore _stateStore = new AttributeOwnerStateStore();
        private long _revision;

        public AttributeService(
            IEnumerable<AttributeDefinition> attributeDefinitions = null,
            IEnumerable<ResourceDefinition> resourceDefinitions = null)
        {
            _calculator = new AttributeCalculator(_definitionRegistry);
            SetAttributeDefinitions(attributeDefinitions, false);
            SetResourceDefinitions(resourceDefinitions, false);
        }

        /// <inheritdoc />
        public long Revision => _revision;

        /// <summary>
        /// 最近一次导入产生的迁移警告。
        /// </summary>
        public string[] LastImportWarnings { get; private set; } = Array.Empty<string>();

        /// <summary>
        /// 替换属性定义。
        /// 已存在 Actor 会补齐新增属性并重算最终值。
        /// </summary>
        public void SetAttributeDefinitions(IEnumerable<AttributeDefinition> definitions)
        {
            SetAttributeDefinitions(definitions, true);
        }

        /// <summary>
        /// 替换资源定义。
        /// 已存在 Actor 会补齐新增资源并刷新资源最大值绑定。
        /// </summary>
        public void SetResourceDefinitions(IEnumerable<ResourceDefinition> definitions)
        {
            SetResourceDefinitions(definitions, true);
        }

        /// <inheritdoc />
        public bool HasActor(string actorId)
        {
            return _stateStore.TryGetOwner(actorId, out _);
        }

        /// <inheritdoc />
        public bool HasAttribute(string actorId, string attributeId)
        {
            return _stateStore.TryGetOwner(actorId, out var owner)
                   && AttributeOwnerStateStore.FindAttribute(owner, attributeId) != null;
        }

        /// <inheritdoc />
        public bool HasResource(string actorId, string resourceId)
        {
            return _stateStore.TryGetOwner(actorId, out var owner)
                   && AttributeOwnerStateStore.FindResource(owner, resourceId) != null;
        }

        /// <inheritdoc />
        public bool TryGetFinalValue(string actorId, string attributeId, out float value)
        {
            value = 0f;
            if (!_stateStore.TryGetOwner(actorId, out var owner))
            {
                return false;
            }

            var state = AttributeOwnerStateStore.FindAttribute(owner, attributeId);
            if (state == null)
            {
                return false;
            }

            value = state.FinalValue;
            return true;
        }

        /// <inheritdoc />
        public float GetFinalValue(string actorId, string attributeId, float fallback = 0f)
        {
            return TryGetFinalValue(actorId, attributeId, out var value) ? value : fallback;
        }

        /// <inheritdoc />
        public bool TryGetBaseValue(string actorId, string attributeId, out float value)
        {
            value = 0f;
            if (!_stateStore.TryGetOwner(actorId, out var owner))
            {
                return false;
            }

            var state = AttributeOwnerStateStore.FindAttribute(owner, attributeId);
            if (state == null)
            {
                return false;
            }

            value = state.BaseValue;
            return true;
        }

        /// <inheritdoc />
        public bool TryGetResource(string actorId, string resourceId, out ResourceValueViewData value)
        {
            value = null;
            if (!_stateStore.TryGetOwner(actorId, out var owner))
            {
                return false;
            }

            var resource = AttributeOwnerStateStore.FindResource(owner, resourceId);
            if (resource == null)
            {
                return false;
            }

            value = BuildResourceViewData(owner.ActorId, resource);
            return true;
        }

        /// <inheritdoc />
        public float GetResourceCurrent(string actorId, string resourceId, float fallback = 0f)
        {
            return TryGetResource(actorId, resourceId, out var value) ? value.CurrentValue : fallback;
        }

        /// <inheritdoc />
        public float GetResourceMax(string actorId, string resourceId, float fallback = 0f)
        {
            return TryGetResource(actorId, resourceId, out var value) ? value.MaxValue : fallback;
        }

        /// <inheritdoc />
        public float GetResourcePercent(string actorId, string resourceId, float fallback = 0f)
        {
            return TryGetResource(actorId, resourceId, out var value) ? value.Percent : fallback;
        }

        /// <inheritdoc />
        public AttributeValueViewData[] GetAllAttributes(string actorId)
        {
            if (!_stateStore.TryGetOwner(actorId, out var owner))
            {
                return Array.Empty<AttributeValueViewData>();
            }

            var attributes = owner.Attributes ?? Array.Empty<AttributeRuntimeState>();
            if (attributes.Length == 0)
            {
                return Array.Empty<AttributeValueViewData>();
            }

            var result = new List<AttributeValueViewData>(attributes.Length);
            for (var i = 0; i < attributes.Length; i++)
            {
                var state = attributes[i];
                if (state != null && !string.IsNullOrWhiteSpace(state.AttributeId))
                {
                    result.Add(BuildAttributeViewData(owner, state));
                }
            }

            return result.Count > 0 ? result.ToArray() : Array.Empty<AttributeValueViewData>();
        }

        /// <inheritdoc />
        public ResourceValueViewData[] GetAllResources(string actorId)
        {
            if (!_stateStore.TryGetOwner(actorId, out var owner))
            {
                return Array.Empty<ResourceValueViewData>();
            }

            var resources = owner.Resources ?? Array.Empty<ResourceRuntimeState>();
            if (resources.Length == 0)
            {
                return Array.Empty<ResourceValueViewData>();
            }

            var result = new List<ResourceValueViewData>(resources.Length);
            for (var i = 0; i < resources.Length; i++)
            {
                var resource = resources[i];
                if (resource != null && !string.IsNullOrWhiteSpace(resource.ResourceId))
                {
                    result.Add(BuildResourceViewData(owner.ActorId, resource));
                }
            }

            return result.Count > 0 ? result.ToArray() : Array.Empty<ResourceValueViewData>();
        }

        /// <inheritdoc />
        public AttributeOperationResult CreateActor(string actorId, string sourceModule = null)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                return AttributeOperationResult.Failed(AttributeFailureReason.InvalidActorId, "ActorId 为空。", actorId, _revision);
            }

            if (_stateStore.TryGetOwner(actorId, out _))
            {
                return AttributeOperationResult.Success(actorId, _revision);
            }

            _resourceIdBuffer.Clear();
            var owner = _stateStore.CreateOwner(actorId, _definitionRegistry, _resourceIdBuffer);
            RecalculateOwnerInternal(owner, _resourceIdBuffer, false, true);
            BumpRevision(owner);
            return AttributeOperationResult.Success(actorId, _revision);
        }

        /// <inheritdoc />
        public AttributeOperationResult RemoveActor(string actorId, string sourceModule = null)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                return AttributeOperationResult.Failed(AttributeFailureReason.InvalidActorId, "ActorId 为空。", actorId, _revision);
            }

            if (!_stateStore.RemoveOwner(actorId))
            {
                return AttributeOperationResult.Failed(AttributeFailureReason.ActorNotFound, "找不到指定 Actor。", actorId, _revision);
            }

            BumpRevision(null);
            return AttributeOperationResult.Success(actorId, _revision);
        }

        /// <inheritdoc />
        public AttributeOperationResult SetBaseValue(string actorId, string attributeId, float value, string sourceModule = null)
        {
            if (!TryGetMutableAttribute(actorId, attributeId, out var owner, out var state, out var failure))
            {
                return failure;
            }

            if (Math.Abs(state.BaseValue - value) <= 0.0001f)
            {
                return AttributeOperationResult.Success(attributeId, _revision);
            }

            state.BaseValue = value;
            state.Revision = NextLocalRevision(state.Revision);
            RecalculateOwnerInternal(owner, null, true, true);
            BumpRevision(owner);
            return AttributeOperationResult.Success(attributeId, _revision);
        }

        /// <inheritdoc />
        public AttributeOperationResult AddModifier(string actorId, AttributeModifier modifier)
        {
            if (!_stateStore.TryGetOwner(actorId, out var owner))
            {
                return AttributeOperationResult.Failed(AttributeFailureReason.ActorNotFound, "找不到指定 Actor。", actorId, _revision);
            }

            if (!AttributeModifierIndex.IsValid(modifier, out var message))
            {
                return AttributeOperationResult.Failed(AttributeFailureReason.InvalidModifier, message, actorId, _revision);
            }

            if (AttributeOwnerStateStore.FindAttribute(owner, modifier.TargetAttributeId) == null)
            {
                return AttributeOperationResult.Failed(
                    AttributeFailureReason.AttributeNotFound,
                    $"找不到目标属性：{modifier.TargetAttributeId}",
                    modifier.TargetAttributeId,
                    _revision);
            }

            AttributeModifierIndex.AddOrReplace(ref owner.Modifiers, modifier, out _);
            RecalculateOwnerInternal(owner, null, true, true);
            BumpRevision(owner);
            return AttributeOperationResult.Success(modifier.TargetAttributeId, _revision);
        }

        /// <inheritdoc />
        public AttributeOperationResult RemoveModifier(string actorId, string sourceId, string modifierId)
        {
            if (!_stateStore.TryGetOwner(actorId, out var owner))
            {
                return AttributeOperationResult.Failed(AttributeFailureReason.ActorNotFound, "找不到指定 Actor。", actorId, _revision);
            }

            if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(modifierId))
            {
                return AttributeOperationResult.Failed(AttributeFailureReason.InvalidModifier, "SourceId 或 ModifierId 为空。", actorId, _revision);
            }

            if (!AttributeModifierIndex.Remove(ref owner.Modifiers, sourceId, modifierId, out var targetAttributeId))
            {
                return AttributeOperationResult.Failed(AttributeFailureReason.InvalidModifier, "找不到指定修饰器。", actorId, _revision);
            }

            RecalculateOwnerInternal(owner, null, true, true);
            BumpRevision(owner);
            return AttributeOperationResult.Success(targetAttributeId, _revision);
        }

        /// <inheritdoc />
        public AttributeOperationResult RemoveModifiersBySource(string actorId, string sourceId)
        {
            if (!_stateStore.TryGetOwner(actorId, out var owner))
            {
                return AttributeOperationResult.Failed(AttributeFailureReason.ActorNotFound, "找不到指定 Actor。", actorId, _revision);
            }

            if (string.IsNullOrWhiteSpace(sourceId))
            {
                return AttributeOperationResult.Failed(AttributeFailureReason.InvalidModifier, "SourceId 为空。", actorId, _revision);
            }

            _affectedAttributeBuffer.Clear();
            if (!AttributeModifierIndex.RemoveBySource(ref owner.Modifiers, sourceId, _affectedAttributeBuffer))
            {
                return AttributeOperationResult.Failed(AttributeFailureReason.InvalidModifier, "找不到指定来源的修饰器。", actorId, _revision);
            }

            RecalculateOwnerInternal(owner, null, true, true);
            BumpRevision(owner);
            return AttributeOperationResult.Success(sourceId, _revision);
        }

        /// <inheritdoc />
        public AttributeOperationResult ConsumeResource(string actorId, string resourceId, float amount, string sourceModule = null)
        {
            if (amount <= 0f)
            {
                return AttributeOperationResult.Failed(AttributeFailureReason.InvalidAmount, "资源消耗量必须大于 0。", resourceId, _revision);
            }

            if (!TryGetMutableResource(actorId, resourceId, out var owner, out var resource, out var failure))
            {
                return failure;
            }

            var nextValue = ResourceRuntimeUpdater.ClampCurrent(resource.CurrentValue - amount, resource.MaxValueCache, false);
            if (Math.Abs(nextValue - resource.CurrentValue) <= 0.0001f)
            {
                return AttributeOperationResult.Success(resourceId, _revision);
            }

            resource.CurrentValue = nextValue;
            if (_definitionRegistry.TryGetResource(resource.ResourceId, out var definition))
            {
                resource.RecoveryDelayTimer = Math.Max(0f, definition.RecoveryDelaySeconds);
            }

            resource.Revision = NextLocalRevision(resource.Revision);
            BumpRevision(owner);
            return AttributeOperationResult.Success(resourceId, _revision);
        }

        /// <inheritdoc />
        public AttributeOperationResult RecoverResource(string actorId, string resourceId, float amount, string sourceModule = null)
        {
            if (amount <= 0f)
            {
                return AttributeOperationResult.Failed(AttributeFailureReason.InvalidAmount, "资源恢复量必须大于 0。", resourceId, _revision);
            }

            if (!TryGetMutableResource(actorId, resourceId, out var owner, out var resource, out var failure))
            {
                return failure;
            }

            var nextValue = ResourceRuntimeUpdater.ClampCurrent(resource.CurrentValue + amount, resource.MaxValueCache, false);
            if (Math.Abs(nextValue - resource.CurrentValue) <= 0.0001f)
            {
                return AttributeOperationResult.Success(resourceId, _revision);
            }

            resource.CurrentValue = nextValue;
            resource.Revision = NextLocalRevision(resource.Revision);
            BumpRevision(owner);
            return AttributeOperationResult.Success(resourceId, _revision);
        }

        /// <inheritdoc />
        public AttributeOperationResult SetResourceCurrent(string actorId, string resourceId, float value, string sourceModule = null)
        {
            if (!TryGetMutableResource(actorId, resourceId, out var owner, out var resource, out var failure))
            {
                return failure;
            }

            var nextValue = ResourceRuntimeUpdater.ClampCurrent(value, resource.MaxValueCache, false);
            if (Math.Abs(nextValue - resource.CurrentValue) <= 0.0001f)
            {
                return AttributeOperationResult.Success(resourceId, _revision);
            }

            resource.CurrentValue = nextValue;
            resource.Revision = NextLocalRevision(resource.Revision);
            BumpRevision(owner);
            return AttributeOperationResult.Success(resourceId, _revision);
        }

        /// <inheritdoc />
        public AttributeSaveData ExportSnapshot()
        {
            _stateStore.CopyOwnersTo(_ownerBuffer);
            var owners = new List<AttributeOwnerSnapshot>(_ownerBuffer.Count);
            for (var i = 0; i < _ownerBuffer.Count; i++)
            {
                var owner = _ownerBuffer[i];
                if (owner != null && !string.IsNullOrWhiteSpace(owner.ActorId))
                {
                    owners.Add(BuildOwnerSnapshot(owner));
                }
            }

            return new AttributeSaveData
            {
                Version = CurrentSaveVersion,
                Revision = _revision,
                Owners = owners.Count > 0 ? owners.ToArray() : Array.Empty<AttributeOwnerSnapshot>()
            };
        }

        /// <inheritdoc />
        public AttributeImportResult ImportSnapshot(AttributeSaveData snapshot)
        {
            if (snapshot == null)
            {
                return AttributeImportResult.Failed(AttributeFailureReason.ImportFailed, "导入快照为空。", null, _revision);
            }

            if (snapshot.Version != CurrentSaveVersion)
            {
                return AttributeImportResult.Failed(
                    AttributeFailureReason.ImportFailed,
                    $"不支持的属性存档版本：{snapshot.Version}",
                    null,
                    _revision);
            }

            try
            {
                var nextStore = new AttributeOwnerStateStore();
                var warnings = new List<string>();
                var owners = snapshot.Owners ?? Array.Empty<AttributeOwnerSnapshot>();
                for (var i = 0; i < owners.Length; i++)
                {
                    var owner = AttributeOwnerStateStore.FromSnapshot(owners[i]);
                    if (owner == null)
                    {
                        warnings.Add($"第 {i} 个 Actor 快照无效，已跳过。");
                        continue;
                    }

                    nextStore.AddImportedOwner(owner);
                }

                nextStore.CopyOwnersTo(_ownerBuffer);
                for (var i = 0; i < _ownerBuffer.Count; i++)
                {
                    var owner = _ownerBuffer[i];
                    _resourceIdBuffer.Clear();
                    AppendMigrationWarnings(owner, warnings);
                    nextStore.RefreshOwnerDefinitions(owner, _definitionRegistry, _resourceIdBuffer);
                    RecalculateOwnerInternal(owner, _resourceIdBuffer, false, false);
                }

                _stateStore = nextStore;
                _revision = Math.Max(0L, snapshot.Revision);
                LastImportWarnings = warnings.Count > 0 ? warnings.ToArray() : Array.Empty<string>();
                return AttributeImportResult.Success(LastImportWarnings, _revision);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NiumaAttribute] 导入属性快照异常：{ex}");
                return AttributeImportResult.Failed(AttributeFailureReason.ImportFailed, ex.Message, null, _revision);
            }
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            var changed = false;
            _stateStore.CopyOwnersTo(_ownerBuffer);
            for (var i = 0; i < _ownerBuffer.Count; i++)
            {
                var owner = _ownerBuffer[i];
                if (owner == null)
                {
                    continue;
                }

                _affectedAttributeBuffer.Clear();
                if (AttributeModifierIndex.Tick(ref owner.Modifiers, deltaTime, _affectedAttributeBuffer))
                {
                    RecalculateOwnerInternal(owner, null, true, false);
                    owner.Revision = NextLocalRevision(owner.Revision);
                    changed = true;
                }

                if (_resourceUpdater.TickAutoRecovery(owner, _definitionRegistry, deltaTime))
                {
                    owner.Revision = NextLocalRevision(owner.Revision);
                    changed = true;
                }
            }

            if (changed)
            {
                BumpRevision(null);
            }
        }

        /// <inheritdoc />
        public void RecalculateActor(string actorId)
        {
            if (!_stateStore.TryGetOwner(actorId, out var owner))
            {
                return;
            }

            _resourceIdBuffer.Clear();
            _stateStore.RefreshOwnerDefinitions(owner, _definitionRegistry, _resourceIdBuffer);
            RecalculateOwnerInternal(owner, _resourceIdBuffer, true, true);
            BumpRevision(owner);
        }

        /// <inheritdoc />
        public void RecalculateAll()
        {
            _stateStore.CopyOwnersTo(_ownerBuffer);
            for (var i = 0; i < _ownerBuffer.Count; i++)
            {
                var owner = _ownerBuffer[i];
                if (owner == null)
                {
                    continue;
                }

                _resourceIdBuffer.Clear();
                _stateStore.RefreshOwnerDefinitions(owner, _definitionRegistry, _resourceIdBuffer);
                RecalculateOwnerInternal(owner, _resourceIdBuffer, true, true);
            }

            BumpRevision(null);
        }

        private void SetAttributeDefinitions(IEnumerable<AttributeDefinition> definitions, bool refreshOwners)
        {
            _definitionRegistry.SetAttributeDefinitions(definitions);
            if (refreshOwners)
            {
                RefreshAllOwnersAfterConfigChanged();
            }
        }

        private void SetResourceDefinitions(IEnumerable<ResourceDefinition> definitions, bool refreshOwners)
        {
            _definitionRegistry.SetResourceDefinitions(definitions);
            if (refreshOwners)
            {
                RefreshAllOwnersAfterConfigChanged();
            }
        }

        private void RefreshAllOwnersAfterConfigChanged()
        {
            if (_stateStore.Count == 0)
            {
                return;
            }

            RecalculateAll();
        }

        private bool RecalculateOwnerInternal(
            AttributeOwnerRuntimeState owner,
            IReadOnlyList<string> initializeResourceIds,
            bool preserveResourcePercent,
            bool logErrors)
        {
            if (owner == null)
            {
                return false;
            }

            if (!_calculator.Recalculate(owner, out var error))
            {
                if (logErrors)
                {
                    Debug.LogWarning($"[NiumaAttribute] Actor={owner.ActorId} 属性重算失败：{error}");
                }

                return false;
            }

            _resourceUpdater.RefreshMaxValues(owner, _definitionRegistry, initializeResourceIds, preserveResourcePercent);
            return true;
        }

        private AttributeOwnerSnapshot BuildOwnerSnapshot(AttributeOwnerRuntimeState owner)
        {
            return new AttributeOwnerSnapshot
            {
                ActorId = owner.ActorId,
                Attributes = AttributeSnapshotUtility.ToAttributeSnapshots(owner.Attributes),
                Resources = BuildResourceSnapshots(owner),
                PersistentModifiers = AttributeSnapshotUtility.ToPersistentModifierSnapshots(owner.Modifiers)
            };
        }

        private ResourceValueSnapshot[] BuildResourceSnapshots(AttributeOwnerRuntimeState owner)
        {
            var resources = owner.Resources ?? Array.Empty<ResourceRuntimeState>();
            if (resources.Length == 0)
            {
                return Array.Empty<ResourceValueSnapshot>();
            }

            var result = new List<ResourceValueSnapshot>(resources.Length);
            for (var i = 0; i < resources.Length; i++)
            {
                var resource = resources[i];
                if (resource == null || string.IsNullOrWhiteSpace(resource.ResourceId))
                {
                    continue;
                }

                if (_definitionRegistry.TryGetResource(resource.ResourceId, out var definition)
                    && !definition.SaveCurrentValue)
                {
                    continue;
                }

                result.Add(resource.ToSnapshot());
            }

            return result.Count > 0 ? result.ToArray() : Array.Empty<ResourceValueSnapshot>();
        }

        private AttributeValueViewData BuildAttributeViewData(AttributeOwnerRuntimeState owner, AttributeRuntimeState state)
        {
            var hasDefinition = _definitionRegistry.TryGetAttribute(state.AttributeId, out var definition);
            return new AttributeValueViewData
            {
                ActorId = owner.ActorId,
                AttributeId = state.AttributeId,
                DisplayName = hasDefinition && !string.IsNullOrWhiteSpace(definition.DisplayName)
                    ? definition.DisplayName
                    : state.AttributeId,
                Kind = hasDefinition ? definition.Kind : AttributeKind.None,
                DominantLayer = _calculator.FindDominantLayer(owner, state.AttributeId),
                BaseValue = state.BaseValue,
                FinalValue = state.FinalValue,
                ModifierDelta = state.FinalValue - state.BaseValue,
                Layers = _calculator.BuildLayerViewData(owner, state.AttributeId),
                IsMissingDefinition = !hasDefinition
            };
        }

        private ResourceValueViewData BuildResourceViewData(string actorId, ResourceRuntimeState resource)
        {
            var hasDefinition = _definitionRegistry.TryGetResource(resource.ResourceId, out var definition);
            var max = resource.MaxValueCache;
            return new ResourceValueViewData
            {
                ActorId = actorId,
                ResourceId = resource.ResourceId,
                DisplayName = hasDefinition && !string.IsNullOrWhiteSpace(definition.DisplayName)
                    ? definition.DisplayName
                    : resource.ResourceId,
                CurrentValue = resource.CurrentValue,
                MaxValue = max,
                Percent = max > 0f ? resource.CurrentValue / max : 0f,
                IsRecovering = hasDefinition
                              && definition.EnableAutoRecovery
                              && definition.RecoveryPerSecond > 0f
                              && resource.CurrentValue < max,
                RecoveryPerSecond = hasDefinition ? definition.RecoveryPerSecond : 0f,
                IsMissingDefinition = !hasDefinition
            };
        }

        private bool TryGetMutableAttribute(
            string actorId,
            string attributeId,
            out AttributeOwnerRuntimeState owner,
            out AttributeRuntimeState state,
            out AttributeOperationResult failure)
        {
            owner = null;
            state = null;
            failure = null;

            if (string.IsNullOrWhiteSpace(actorId))
            {
                failure = AttributeOperationResult.Failed(AttributeFailureReason.InvalidActorId, "ActorId 为空。", actorId, _revision);
                return false;
            }

            if (string.IsNullOrWhiteSpace(attributeId))
            {
                failure = AttributeOperationResult.Failed(AttributeFailureReason.InvalidAttributeId, "AttributeId 为空。", attributeId, _revision);
                return false;
            }

            if (!_stateStore.TryGetOwner(actorId, out owner))
            {
                failure = AttributeOperationResult.Failed(AttributeFailureReason.ActorNotFound, "找不到指定 Actor。", actorId, _revision);
                return false;
            }

            state = AttributeOwnerStateStore.FindAttribute(owner, attributeId);
            if (state == null)
            {
                failure = AttributeOperationResult.Failed(AttributeFailureReason.AttributeNotFound, $"找不到属性：{attributeId}", attributeId, _revision);
                return false;
            }

            return true;
        }

        private bool TryGetMutableResource(
            string actorId,
            string resourceId,
            out AttributeOwnerRuntimeState owner,
            out ResourceRuntimeState resource,
            out AttributeOperationResult failure)
        {
            owner = null;
            resource = null;
            failure = null;

            if (string.IsNullOrWhiteSpace(actorId))
            {
                failure = AttributeOperationResult.Failed(AttributeFailureReason.InvalidActorId, "ActorId 为空。", actorId, _revision);
                return false;
            }

            if (string.IsNullOrWhiteSpace(resourceId))
            {
                failure = AttributeOperationResult.Failed(AttributeFailureReason.InvalidResourceId, "ResourceId 为空。", resourceId, _revision);
                return false;
            }

            if (!_stateStore.TryGetOwner(actorId, out owner))
            {
                failure = AttributeOperationResult.Failed(AttributeFailureReason.ActorNotFound, "找不到指定 Actor。", actorId, _revision);
                return false;
            }

            resource = AttributeOwnerStateStore.FindResource(owner, resourceId);
            if (resource == null)
            {
                failure = AttributeOperationResult.Failed(AttributeFailureReason.ResourceNotFound, $"找不到资源：{resourceId}", resourceId, _revision);
                return false;
            }

            return true;
        }

        private void AppendMigrationWarnings(AttributeOwnerRuntimeState owner, List<string> warnings)
        {
            if (owner == null || warnings == null)
            {
                return;
            }

            var attributes = owner.Attributes ?? Array.Empty<AttributeRuntimeState>();
            for (var i = 0; i < attributes.Length; i++)
            {
                var state = attributes[i];
                if (state != null
                    && !string.IsNullOrWhiteSpace(state.AttributeId)
                    && !_definitionRegistry.TryGetAttribute(state.AttributeId, out _))
                {
                    warnings.Add($"Actor={owner.ActorId} 的属性配置缺失：{state.AttributeId}");
                }
            }

            var resources = owner.Resources ?? Array.Empty<ResourceRuntimeState>();
            for (var i = 0; i < resources.Length; i++)
            {
                var state = resources[i];
                if (state == null || string.IsNullOrWhiteSpace(state.ResourceId))
                {
                    continue;
                }

                if (!_definitionRegistry.TryGetResource(state.ResourceId, out var definition))
                {
                    warnings.Add($"Actor={owner.ActorId} 的资源配置缺失：{state.ResourceId}");
                    continue;
                }

                if (!string.Equals(state.MaxAttributeId, definition.MaxAttributeId, StringComparison.Ordinal))
                {
                    warnings.Add($"Actor={owner.ActorId} 的资源 {state.ResourceId} 最大值绑定已从 {state.MaxAttributeId} 刷新为 {definition.MaxAttributeId}。");
                }
            }
        }

        private void BumpRevision(AttributeOwnerRuntimeState owner)
        {
            _revision = NextLocalRevision(_revision);
            if (owner != null)
            {
                owner.Revision = NextLocalRevision(owner.Revision);
            }
        }

        private static long NextLocalRevision(long current)
        {
            return current == long.MaxValue ? long.MaxValue : current + 1L;
        }
    }
}
