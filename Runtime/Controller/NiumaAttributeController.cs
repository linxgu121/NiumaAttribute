using System;
using NiumaAttribute.Config;
using NiumaAttribute.Data;
using NiumaAttribute.Enum;
using NiumaAttribute.Result;
using NiumaAttribute.Service;
using NiumaCore.Module;
using UnityEngine;

namespace NiumaAttribute.Controller
{
    /// <summary>
    /// NiumaAttribute 属性模块根控制器。
    /// 负责把纯 C# 的 AttributeService 接入 Unity 生命周期、Inspector 配置、GameContext 和基础调试入口。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NiumaAttributeController : MonoBehaviour, IGameModule
    {
        [Header("属性配置")]
        [Tooltip("属性定义列表。请拖入当前版本可用的 AttributeDefinition，AttributeId 必须稳定。")]
        [SerializeField] private AttributeDefinition[] attributeDefinitions = Array.Empty<AttributeDefinition>();

        [Tooltip("资源定义列表。请拖入 HP、MP、Stamina 等 ResourceDefinition，ResourceId 必须稳定。")]
        [SerializeField] private ResourceDefinition[] resourceDefinitions = Array.Empty<ResourceDefinition>();

        [Header("模块启动")]
        [Tooltip("Awake 时是否自动初始化属性服务。没有统一模块启动器时建议开启。")]
        [SerializeField] private bool initializeOnAwake = true;

        [Tooltip("OnEnable 时是否自动启动属性模块。没有统一模块启动器时建议开启。")]
        [SerializeField] private bool startOnEnable = true;

        [Tooltip("初始化时是否把 IAttributeService、IAttributeQuery、IAttributeCommand 注册到 GameContext。使用统一 GameContext 的项目建议开启。")]
        [SerializeField] private bool registerServiceToContext = true;

        [Tooltip("是否由本控制器的 Update 自动驱动 Tick。若项目已有统一模块启动器调用 IGameModule.Tick，请关闭，避免 Modifier 过期和资源恢复每帧执行两次。")]
        [SerializeField] private bool driveTickInUpdate = true;

        [Header("调试：Actor")]
        [Tooltip("调试用 ActorId。玩家建议使用 player，本地多玩家或 NPC 请使用稳定唯一 ID。")]
        [SerializeField] private string debugActorId = "player";

        [Header("调试：属性")]
        [Tooltip("调试用属性 ID。右键菜单设置基础值或查询最终值时使用。")]
        [SerializeField] private string debugAttributeId;

        [Tooltip("调试基础属性值。右键菜单设置基础值时使用。")]
        [SerializeField] private float debugBaseValue;

        [Header("调试：资源")]
        [Tooltip("调试用资源 ID，例如 health、mp、stamina。")]
        [SerializeField] private string debugResourceId;

        [Tooltip("调试资源变化量。右键菜单消耗或恢复资源时使用，必须大于 0。")]
        [SerializeField] private float debugResourceAmount = 10f;

        [Header("调试：Modifier")]
        [Tooltip("调试修饰器来源 ID。例如 debug:inspector。")]
        [SerializeField] private string debugModifierSourceId = "debug:inspector";

        [Tooltip("调试修饰器 ID。同一 SourceId + ModifierId 会覆盖旧修饰器。")]
        [SerializeField] private string debugModifierId = "debug_modifier";

        [Tooltip("调试修饰器运算类型。")]
        [SerializeField] private AttributeModifierOperation debugModifierOperation = AttributeModifierOperation.Add;

        [Tooltip("调试修饰器分层。")]
        [SerializeField] private AttributeModifierLayer debugModifierLayer = AttributeModifierLayer.Debug;

        [Tooltip("调试修饰器数值。")]
        [SerializeField] private float debugModifierValue = 5f;

        [Tooltip("调试修饰器持续时间。小于等于 0 表示永久。")]
        [SerializeField] private float debugModifierDurationSeconds;

        [Tooltip("调试修饰器是否进入存档。")]
        [SerializeField] private bool debugModifierPersistent;

        private IAttributeService _attributeService;
        private IAttributeConfigurationService _configurationService;
        private GameContext _context;
        private bool _warnedMissingAttributeDefinitions;
        private bool _warnedMissingResourceDefinitions;
        private bool _warnedInitializeFailure;
        private bool _warnedServiceNotReady;
        private bool _autoInitializeFailed;
        private bool _isDestroyed;

        /// <summary>
        /// 模块名称。
        /// </summary>
        public string ModuleName => "NiumaAttribute";

        /// <summary>
        /// 属性服务门面接口。
        /// 外部模块应优先依赖 IAttributeQuery 或 IAttributeCommand，而不是直接依赖 AttributeService 实现。
        /// </summary>
        public IAttributeService AttributeService => _attributeService;

        /// <summary>
        /// 属性查询接口。
        /// UI、战斗、技能、任务条件等只读模块优先依赖该接口。
        /// </summary>
        public IAttributeQuery AttributeQuery => _attributeService;

        /// <summary>
        /// 属性命令接口。
        /// 装备、效果、技能消耗等需要修改属性或资源的模块依赖该接口。
        /// </summary>
        public IAttributeCommand AttributeCommand => _attributeService;

        /// <summary>
        /// 当前模块是否已经初始化。
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 当前模块是否正在运行。
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// 属性模块全局修订号。
        /// UI、存档或调试桥接层可通过该值判断是否需要重新拉取快照。
        /// </summary>
        public long AttributeRevision => _attributeService != null ? _attributeService.Revision : 0L;

        /// <summary>
        /// 当前属性定义配置。
        /// 外部只读使用，正式修改请通过 SetAttributeDefinitions。
        /// </summary>
        public AttributeDefinition[] AttributeDefinitions => attributeDefinitions ?? Array.Empty<AttributeDefinition>();

        /// <summary>
        /// 当前资源定义配置。
        /// 外部只读使用，正式修改请通过 SetResourceDefinitions。
        /// </summary>
        public ResourceDefinition[] ResourceDefinitions => resourceDefinitions ?? Array.Empty<ResourceDefinition>();

        /// <summary>
        /// 最近一次属性命令操作结果。
        /// </summary>
        public AttributeOperationResult LastOperationResult { get; private set; }

        /// <summary>
        /// 最近一次导入结果。
        /// </summary>
        public AttributeImportResult LastImportResult { get; private set; }

        private void Awake()
        {
            if (initializeOnAwake && !IsInitialized)
            {
                Initialize(null);
            }
        }

        private void OnEnable()
        {
            if (startOnEnable && IsInitialized && !IsRunning)
            {
                StartModule();
            }
        }

        private void OnDisable()
        {
            if (IsRunning)
            {
                StopModule();
            }
        }

        private void OnDestroy()
        {
            UnregisterServicesFromContext();
            IsRunning = false;
            IsInitialized = false;
            _isDestroyed = true;
            _attributeService = null;
            _configurationService = null;
        }

        /// <summary>
        /// 初始化属性模块。
        /// 如果已有服务，会导出属性快照并在新服务中恢复，避免重复初始化丢失 Actor 数值状态。
        /// </summary>
        public void Initialize(GameContext context)
        {
            var wasRunning = IsRunning;
            var previousService = _attributeService;
            var previousConfigurationService = _configurationService;
            var previousContext = _context;
            var previousInitialized = IsInitialized;
            var targetContext = context ?? _context;
            var previousRegisteredService = targetContext != null ? targetContext.GetService<IAttributeService>() : null;
            var previousRegisteredQuery = targetContext != null ? targetContext.GetService<IAttributeQuery>() : null;
            var previousRegisteredCommand = targetContext != null ? targetContext.GetService<IAttributeCommand>() : null;
            var initializedSuccessfully = false;
            AttributeService newService = null;
            IsRunning = false;

            try
            {
                _context = targetContext;
                WarnIfConfigMissing();

                var snapshot = previousService != null ? previousService.ExportSnapshot() : null;
                newService = new AttributeService(attributeDefinitions, resourceDefinitions);
                if (snapshot != null)
                {
                    LastImportResult = newService.ImportSnapshot(snapshot);
                }

                _attributeService = newService;
                _configurationService = newService;
                RegisterServicesToContext();
                IsInitialized = true;
                _warnedInitializeFailure = false;
                _autoInitializeFailed = false;
                _warnedServiceNotReady = false;
                initializedSuccessfully = true;
            }
            catch (Exception exception)
            {
                if (!_warnedInitializeFailure)
                {
                    Debug.LogError($"[NiumaAttribute] 初始化属性模块失败：{exception.Message}", this);
                    _warnedInitializeFailure = true;
                }

                RestoreRegisteredAttributeServices(targetContext, previousRegisteredService, previousRegisteredQuery, previousRegisteredCommand, newService);
                _attributeService = previousService;
                _configurationService = previousConfigurationService;
                _context = previousContext;
                IsInitialized = previousInitialized;
            }
            finally
            {
                IsRunning = initializedSuccessfully
                    ? wasRunning && _attributeService != null
                    : wasRunning && previousInitialized && previousService != null;
            }
        }

        /// <summary>
        /// 启动属性模块。
        /// </summary>
        public void StartModule()
        {
            if (!IsInitialized)
            {
                Initialize(_context);
            }

            IsRunning = _attributeService != null;
        }

        /// <summary>
        /// 停止属性模块。
        /// 停止只影响 Tick 驱动，不会清空属性数据，也不会导出存档。
        /// </summary>
        public void StopModule()
        {
            IsRunning = false;
        }

        /// <summary>
        /// 模块帧更新。
        /// 驱动 Modifier 过期和资源基础自动恢复。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!IsRunning || _attributeService == null)
            {
                return;
            }

            _attributeService.Tick(deltaTime);
        }

        private void Update()
        {
            if (!driveTickInUpdate)
            {
                return;
            }

            Tick(Time.deltaTime);
        }

        /// <summary>
        /// 替换属性定义并刷新服务配置。
        /// </summary>
        public void SetAttributeDefinitions(AttributeDefinition[] definitions)
        {
            attributeDefinitions = definitions ?? Array.Empty<AttributeDefinition>();
            _warnedMissingAttributeDefinitions = false;
            if (_configurationService != null)
            {
                _configurationService.SetAttributeDefinitions(attributeDefinitions);
            }
        }

        /// <summary>
        /// 替换资源定义并刷新服务配置。
        /// </summary>
        public void SetResourceDefinitions(ResourceDefinition[] definitions)
        {
            resourceDefinitions = definitions ?? Array.Empty<ResourceDefinition>();
            _warnedMissingResourceDefinitions = false;
            if (_configurationService != null)
            {
                _configurationService.SetResourceDefinitions(resourceDefinitions);
            }
        }

        /// <summary>
        /// 导出属性模块快照。
        /// 存档适配器后续会通过该入口收集数据。
        /// </summary>
        public AttributeSaveData ExportSnapshot()
        {
            return EnsureServiceReady(false) ? _attributeService.ExportSnapshot() : new AttributeSaveData();
        }

        /// <summary>
        /// 导入属性模块快照。
        /// </summary>
        public AttributeImportResult ImportSnapshot(AttributeSaveData snapshot)
        {
            if (snapshot == null)
            {
                LastImportResult = AttributeImportResult.Failed(AttributeFailureReason.ImportFailed, "导入快照为空。", null, AttributeRevision);
                return LastImportResult;
            }

            if (!EnsureServiceReady(true))
            {
                LastImportResult = AttributeImportResult.Failed(AttributeFailureReason.ServiceNotReady, "属性服务尚未准备好。", null, AttributeRevision);
                return LastImportResult;
            }

            LastImportResult = _attributeService.ImportSnapshot(snapshot);
            return LastImportResult;
        }

        public bool HasActor(string actorId)
        {
            return EnsureServiceReady(true) && _attributeService.HasActor(actorId);
        }

        public bool TryGetFinalValue(string actorId, string attributeId, out float value)
        {
            value = 0f;
            return EnsureServiceReady(true) && _attributeService.TryGetFinalValue(actorId, attributeId, out value);
        }

        public bool TryGetResource(string actorId, string resourceId, out NiumaAttribute.ViewData.ResourceValueViewData value)
        {
            value = null;
            return EnsureServiceReady(true) && _attributeService.TryGetResource(actorId, resourceId, out value);
        }

        public AttributeOperationResult CreateActor(string actorId)
        {
            if (!EnsureServiceReady(true))
            {
                LastOperationResult = AttributeOperationResult.Failed(AttributeFailureReason.ServiceNotReady, "属性服务尚未准备好。", actorId, AttributeRevision);
                return LastOperationResult;
            }

            LastOperationResult = _attributeService.CreateActor(actorId, ModuleName);
            return LastOperationResult;
        }

        public AttributeOperationResult SetBaseValue(string actorId, string attributeId, float value)
        {
            if (!EnsureServiceReady(true))
            {
                LastOperationResult = AttributeOperationResult.Failed(AttributeFailureReason.ServiceNotReady, "属性服务尚未准备好。", attributeId, AttributeRevision);
                return LastOperationResult;
            }

            LastOperationResult = _attributeService.SetBaseValue(actorId, attributeId, value, ModuleName);
            return LastOperationResult;
        }

        public AttributeOperationResult ConsumeResource(string actorId, string resourceId, float amount)
        {
            if (!EnsureServiceReady(true))
            {
                LastOperationResult = AttributeOperationResult.Failed(AttributeFailureReason.ServiceNotReady, "属性服务尚未准备好。", resourceId, AttributeRevision);
                return LastOperationResult;
            }

            LastOperationResult = _attributeService.ConsumeResource(actorId, resourceId, amount, ModuleName);
            return LastOperationResult;
        }

        public AttributeOperationResult RecoverResource(string actorId, string resourceId, float amount)
        {
            if (!EnsureServiceReady(true))
            {
                LastOperationResult = AttributeOperationResult.Failed(AttributeFailureReason.ServiceNotReady, "属性服务尚未准备好。", resourceId, AttributeRevision);
                return LastOperationResult;
            }

            LastOperationResult = _attributeService.RecoverResource(actorId, resourceId, amount, ModuleName);
            return LastOperationResult;
        }

        public AttributeOperationResult AddModifier(string actorId, AttributeModifier modifier)
        {
            if (!EnsureServiceReady(true))
            {
                LastOperationResult = AttributeOperationResult.Failed(AttributeFailureReason.ServiceNotReady, "属性服务尚未准备好。", actorId, AttributeRevision);
                return LastOperationResult;
            }

            LastOperationResult = _attributeService.AddModifier(actorId, modifier);
            return LastOperationResult;
        }

        [ContextMenu("NiumaAttribute/重新初始化服务")]
        private void DebugReinitialize()
        {
            Initialize(_context);
            if (IsInitialized)
            {
                Debug.Log("[NiumaAttribute] 重新初始化完成。", this);
            }
        }

        [ContextMenu("NiumaAttribute/创建调试 Actor")]
        private void DebugCreateActor()
        {
            var result = CreateActor(debugActorId);
            Debug.Log($"[NiumaAttribute] 创建 Actor：Succeeded={result.Succeeded}, Reason={result.FailureReason}, Revision={result.Revision}, Message={result.Message}", this);
        }

        [ContextMenu("NiumaAttribute/设置调试属性基础值")]
        private void DebugSetBaseValue()
        {
            var result = SetBaseValue(debugActorId, debugAttributeId, debugBaseValue);
            Debug.Log($"[NiumaAttribute] 设置基础值：Succeeded={result.Succeeded}, Reason={result.FailureReason}, Revision={result.Revision}, Message={result.Message}", this);
        }

        [ContextMenu("NiumaAttribute/打印调试属性最终值")]
        private void DebugPrintFinalValue()
        {
            if (TryGetFinalValue(debugActorId, debugAttributeId, out var value))
            {
                Debug.Log($"[NiumaAttribute] Actor={debugActorId}, Attribute={debugAttributeId}, FinalValue={value}", this);
                return;
            }

            Debug.LogWarning($"[NiumaAttribute] 无法查询最终值：Actor={debugActorId}, Attribute={debugAttributeId}", this);
        }

        [ContextMenu("NiumaAttribute/消耗调试资源")]
        private void DebugConsumeResource()
        {
            var result = ConsumeResource(debugActorId, debugResourceId, debugResourceAmount);
            Debug.Log($"[NiumaAttribute] 消耗资源：Succeeded={result.Succeeded}, Reason={result.FailureReason}, Revision={result.Revision}, Message={result.Message}", this);
        }

        [ContextMenu("NiumaAttribute/恢复调试资源")]
        private void DebugRecoverResource()
        {
            var result = RecoverResource(debugActorId, debugResourceId, debugResourceAmount);
            Debug.Log($"[NiumaAttribute] 恢复资源：Succeeded={result.Succeeded}, Reason={result.FailureReason}, Revision={result.Revision}, Message={result.Message}", this);
        }

        [ContextMenu("NiumaAttribute/添加调试 Modifier")]
        private void DebugAddModifier()
        {
            var modifier = new AttributeModifier
            {
                SourceId = debugModifierSourceId,
                ModifierId = debugModifierId,
                TargetAttributeId = debugAttributeId,
                Operation = debugModifierOperation,
                Layer = debugModifierLayer,
                Value = debugModifierValue,
                DurationSeconds = debugModifierDurationSeconds,
                IsPersistent = debugModifierPersistent,
                SourceModule = ModuleName
            };

            var result = AddModifier(debugActorId, modifier);
            Debug.Log($"[NiumaAttribute] 添加 Modifier：Succeeded={result.Succeeded}, Reason={result.FailureReason}, Revision={result.Revision}, Message={result.Message}", this);
        }

        [ContextMenu("NiumaAttribute/移除调试 Modifier 来源")]
        private void DebugRemoveModifierSource()
        {
            if (!EnsureServiceReady(true))
            {
                Debug.LogWarning("[NiumaAttribute] 属性服务尚未准备好。", this);
                return;
            }

            LastOperationResult = _attributeService.RemoveModifiersBySource(debugActorId, debugModifierSourceId);
            Debug.Log($"[NiumaAttribute] 移除 Modifier 来源：Succeeded={LastOperationResult.Succeeded}, Reason={LastOperationResult.FailureReason}, Revision={LastOperationResult.Revision}, Message={LastOperationResult.Message}", this);
        }

        [ContextMenu("NiumaAttribute/重算全部 Actor")]
        private void DebugRecalculateAll()
        {
            if (!EnsureServiceReady(true))
            {
                Debug.LogWarning("[NiumaAttribute] 属性服务尚未准备好。", this);
                return;
            }

            _attributeService.RecalculateAll();
            Debug.Log($"[NiumaAttribute] 已重算全部 Actor，Revision={AttributeRevision}", this);
        }

        private bool EnsureServiceReady(bool allowAutoInitialize)
        {
            if (_attributeService != null)
            {
                return true;
            }

            if (_isDestroyed || !allowAutoInitialize || _autoInitializeFailed)
            {
                WarnServiceNotReady();
                return false;
            }

            Initialize(_context);
            if (_attributeService != null)
            {
                return true;
            }

            _autoInitializeFailed = true;
            WarnServiceNotReady();
            return false;
        }

        private void RegisterServicesToContext()
        {
            if (!registerServiceToContext || _context == null || _attributeService == null)
            {
                return;
            }

            _context.RegisterService<IAttributeService>(_attributeService);
            _context.RegisterService<IAttributeQuery>(_attributeService);
            _context.RegisterService<IAttributeCommand>(_attributeService);
        }

        private void UnregisterServicesFromContext()
        {
            if (!registerServiceToContext || _context == null || _attributeService == null)
            {
                return;
            }

            ClearRegisteredServiceIfCurrent<IAttributeService>(_context, _attributeService);
            ClearRegisteredServiceIfCurrent<IAttributeQuery>(_context, _attributeService);
            ClearRegisteredServiceIfCurrent<IAttributeCommand>(_context, _attributeService);
        }

        private static void RestoreRegisteredAttributeServices(
            GameContext context,
            IAttributeService previousService,
            IAttributeQuery previousQuery,
            IAttributeCommand previousCommand,
            AttributeService failedService)
        {
            if (context == null)
            {
                return;
            }

            RestoreRegisteredService(context, previousService, failedService);
            RestoreRegisteredService(context, previousQuery, failedService);
            RestoreRegisteredService(context, previousCommand, failedService);
        }

        private static void RestoreRegisteredService<T>(GameContext context, T previousService, AttributeService failedService)
            where T : class
        {
            if (context == null)
            {
                return;
            }

            var current = context.GetService<T>();
            if (failedService != null && ReferenceEquals(current, failedService))
            {
                context.UnregisterService<T>();
            }

            if (previousService != null)
            {
                context.RegisterService(previousService);
            }
        }

        private static void ClearRegisteredServiceIfCurrent<T>(GameContext context, T service)
            where T : class
        {
            if (context == null || service == null)
            {
                return;
            }

            var current = context.GetService<T>();
            if (ReferenceEquals(current, service))
            {
                context.UnregisterService<T>();
            }
        }

        private void WarnIfConfigMissing()
        {
            if ((attributeDefinitions == null || attributeDefinitions.Length == 0) && !_warnedMissingAttributeDefinitions)
            {
                Debug.LogWarning("[NiumaAttribute] 未配置任何 AttributeDefinition，Actor 将没有属性定义可初始化。", this);
                _warnedMissingAttributeDefinitions = true;
            }

            if ((resourceDefinitions == null || resourceDefinitions.Length == 0) && !_warnedMissingResourceDefinitions)
            {
                Debug.LogWarning("[NiumaAttribute] 未配置任何 ResourceDefinition，Actor 将没有 HP/MP/Stamina 等资源可初始化。", this);
                _warnedMissingResourceDefinitions = true;
            }
        }

        private void WarnServiceNotReady()
        {
            if (_warnedServiceNotReady)
            {
                return;
            }

            Debug.LogWarning("[NiumaAttribute] 属性服务尚未准备好。", this);
            _warnedServiceNotReady = true;
        }
    }
}
