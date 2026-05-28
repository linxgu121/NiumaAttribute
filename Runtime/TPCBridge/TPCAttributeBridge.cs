using NiumaAttribute.Controller;
using NiumaAttribute.Service;
using NiumaTPC.Character;
using UnityEngine;

namespace NiumaAttribute.TPCBridge
{
    /// <summary>
    /// TPC 到 Attribute 的过渡桥接器。
    /// 第一版只读取 TPC 的生命和体力，再写入 Attribute，TPC 仍然是角色控制的权威来源。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TPCAttributeBridge : MonoBehaviour
    {
        private const string SourceModule = "NiumaTPC.AttributeBridge";

        [Header("模块引用")]
        [Tooltip("属性模块根控制器。未绑定时会在场景中自动查找 NiumaAttributeController。")]
        [SerializeField] private NiumaAttributeController attributeController;

        [Tooltip("TPC 角色控制器。未绑定时会在本物体、父物体、子物体或场景中自动查找 NiumaCharacterController。")]
        [SerializeField] private NiumaCharacterController tpcCharacter;

        [Header("Actor 映射")]
        [Tooltip("写入 Attribute 的 ActorId。玩家建议使用 player，本地多玩家或 NPC 请使用稳定唯一 ID。")]
        [SerializeField] private string actorId = "player";

        [Tooltip("Attribute 中不存在该 Actor 时，是否由桥接器自动创建。")]
        [SerializeField] private bool createActorIfMissing = true;

        [Header("资源 ID")]
        [Tooltip("Attribute 中表示生命值的 ResourceId，需要与 ResourceDefinition.ResourceId 保持一致。")]
        [SerializeField] private string healthResourceId = "health";

        [Tooltip("Attribute 中表示体力值的 ResourceId，需要与 ResourceDefinition.ResourceId 保持一致。")]
        [SerializeField] private string staminaResourceId = "stamina";

        [Header("最大值属性 ID")]
        [Tooltip("Attribute 中表示生命上限的 AttributeId，需要与 ResourceDefinition.MaxAttributeId 保持一致。")]
        [SerializeField] private string maxHealthAttributeId = "max_health";

        [Tooltip("Attribute 中表示体力上限的 AttributeId，需要与 ResourceDefinition.MaxAttributeId 保持一致。")]
        [SerializeField] private string maxStaminaAttributeId = "max_stamina";

        [Header("同步开关")]
        [Tooltip("启用时立即执行一次同步，用于进入场景后让 Attribute 拿到 TPC 当前生命和体力。")]
        [SerializeField] private bool syncOnEnable = true;

        [Tooltip("LateUpdate 中持续同步。过渡期建议开启，因为 TPC 的 HealthArbiter/StaminaArbiter 仍然是权威来源。")]
        [SerializeField] private bool syncInLateUpdate = true;

        [Tooltip("是否同步生命当前值和生命上限。关闭后不会写入 health / max_health。")]
        [SerializeField] private bool syncHealth = true;

        [Tooltip("是否同步体力当前值和体力上限。关闭后不会写入 stamina / max_stamina。")]
        [SerializeField] private bool syncStamina = true;

        [Tooltip("是否把 TPC 配置中的最大生命/最大体力同步到 Attribute 基础属性。")]
        [SerializeField] private bool syncMaxAttributes = true;

        [Tooltip("连续同步的最小间隔。0 表示每帧检查一次；数值越大，Attribute UI 变化越不实时。")]
        [Min(0f)]
        [SerializeField] private float syncInterval = 0f;

        [Tooltip("浮点比较容差。TPC 数值变化小于该值时不会重复写入 Attribute，避免无意义 Revision 增长。")]
        [Min(0.00001f)]
        [SerializeField] private float valueEpsilon = 0.001f;

        [Header("调试")]
        [Tooltip("缺少引用、Actor、属性或资源时是否输出警告。正式版本可以关闭以减少日志。")]
        [SerializeField] private bool logWarnings = true;

        private float _nextSyncTime;
        private float _lastHealth = float.NaN;
        private float _lastMaxHealth = float.NaN;
        private float _lastStamina = float.NaN;
        private float _lastMaxStamina = float.NaN;
        private string _lastSyncedActorId;
        private bool _warnedMissingAttributeController;
        private bool _warnedMissingTPCCharacter;
        private bool _warnedMissingActor;
        private bool _warnedMissingHealth;
        private bool _warnedMissingStamina;
        private bool _warnedMissingMaxHealth;
        private bool _warnedMissingMaxStamina;

        private void Awake()
        {
            ResolveReferences(false);
        }

        private void OnEnable()
        {
            _nextSyncTime = 0f;
            if (syncOnEnable)
            {
                SyncNow();
            }
        }

        private void LateUpdate()
        {
            if (!syncInLateUpdate)
            {
                return;
            }

            if (syncInterval > 0f && Time.unscaledTime < _nextSyncTime)
            {
                return;
            }

            SyncNow();
            _nextSyncTime = Time.unscaledTime + syncInterval;
        }

        /// <summary>
        /// 立即把 TPC 当前生命/体力同步到 Attribute。
        /// 该方法只写 Attribute，不会反向修改 TPC 的生命或体力。
        /// </summary>
        [ContextMenu("NiumaAttribute/同步 TPC 生命体力")]
        public void SyncNow()
        {
            if (!EnsureReady())
            {
                return;
            }

            var command = attributeController.AttributeCommand;
            var query = attributeController.AttributeQuery;
            if (command == null || query == null)
            {
                WarnOnce(ref _warnedMissingAttributeController, "属性服务尚未准备好，无法同步 TPC 数值。", this);
                return;
            }

            if (!string.Equals(_lastSyncedActorId, actorId))
            {
                ResetCachedValues();
                _lastSyncedActorId = actorId;
            }

            if (!EnsureActor(query, command))
            {
                return;
            }

            var runtimeData = tpcCharacter.RuntimeData;
            var coreConfig = tpcCharacter.Config != null ? tpcCharacter.Config.Core : null;

            if (syncHealth)
            {
                var maxHealth = coreConfig != null ? coreConfig.MaxHealth : 0f;
                SyncAttributeValue(query, command, maxHealthAttributeId, maxHealth, ref _lastMaxHealth, ref _warnedMissingMaxHealth);
                SyncResourceValue(query, command, healthResourceId, runtimeData.CurrentHealth, ref _lastHealth, ref _warnedMissingHealth);
            }

            if (syncStamina)
            {
                var maxStamina = coreConfig != null ? coreConfig.MaxStamina : 0f;
                SyncAttributeValue(query, command, maxStaminaAttributeId, maxStamina, ref _lastMaxStamina, ref _warnedMissingMaxStamina);
                SyncResourceValue(query, command, staminaResourceId, runtimeData.CurrentStamina, ref _lastStamina, ref _warnedMissingStamina);
            }
        }

        private bool EnsureReady()
        {
            ResolveReferences(logWarnings);

            if (attributeController == null)
            {
                WarnOnce(ref _warnedMissingAttributeController, "未找到 NiumaAttributeController，请在 Inspector 中绑定属性模块根控制器。", this);
                return false;
            }

            if (tpcCharacter == null || tpcCharacter.RuntimeData == null)
            {
                WarnOnce(ref _warnedMissingTPCCharacter, "未找到可用的 NiumaCharacterController，或 TPC RuntimeData 尚未初始化。", this);
                return false;
            }

            if (string.IsNullOrWhiteSpace(actorId))
            {
                WarnOnce(ref _warnedMissingActor, "ActorId 为空，无法把 TPC 数值写入 Attribute。", this);
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

            if (tpcCharacter == null)
            {
                tpcCharacter = GetComponent<NiumaCharacterController>();
            }

            if (tpcCharacter == null)
            {
                tpcCharacter = GetComponentInParent<NiumaCharacterController>();
            }

            if (tpcCharacter == null)
            {
                tpcCharacter = GetComponentInChildren<NiumaCharacterController>();
            }

            if (tpcCharacter == null)
            {
                tpcCharacter = FindObjectOfType<NiumaCharacterController>();
            }

            if (warnMissing && attributeController == null)
            {
                WarnOnce(ref _warnedMissingAttributeController, "自动查找 NiumaAttributeController 失败，请手动绑定。", this);
            }

            if (warnMissing && tpcCharacter == null)
            {
                WarnOnce(ref _warnedMissingTPCCharacter, "自动查找 NiumaCharacterController 失败，请手动绑定。", this);
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

            var result = command.CreateActor(actorId, SourceModule);
            if (result == null || !result.Succeeded)
            {
                WarnOnce(ref _warnedMissingActor, $"创建 Attribute Actor 失败：ActorId={actorId}, Reason={result?.FailureReason}, Message={result?.Message}", this);
                return false;
            }

            // 新 Actor 需要重新写入一次全部数值，不能复用上一个 Actor 的缓存。
            ResetCachedValues();
            return true;
        }

        private void SyncAttributeValue(
            IAttributeQuery query,
            IAttributeCommand command,
            string attributeId,
            float value,
            ref float cachedValue,
            ref bool warnedMissing)
        {
            if (!syncMaxAttributes || string.IsNullOrWhiteSpace(attributeId))
            {
                return;
            }

            if (!query.HasAttribute(actorId, attributeId))
            {
                WarnOnce(ref warnedMissing, $"Attribute 中缺少最大值属性：Actor={actorId}, AttributeId={attributeId}。请确认 AttributeDefinition 与 ResourceDefinition.MaxAttributeId。", this);
                return;
            }

            if (!HasMeaningfulChange(cachedValue, value))
            {
                return;
            }

            var result = command.SetBaseValue(actorId, attributeId, value, SourceModule);
            if (result == null || !result.Succeeded)
            {
                WarnOnce(ref warnedMissing, $"同步 TPC 最大值到 Attribute 失败：AttributeId={attributeId}, Reason={result?.FailureReason}, Message={result?.Message}", this);
                return;
            }

            cachedValue = value;
        }

        private void SyncResourceValue(
            IAttributeQuery query,
            IAttributeCommand command,
            string resourceId,
            float value,
            ref float cachedValue,
            ref bool warnedMissing)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                return;
            }

            if (!query.HasResource(actorId, resourceId))
            {
                WarnOnce(ref warnedMissing, $"Attribute 中缺少资源：Actor={actorId}, ResourceId={resourceId}。请确认 ResourceDefinition 已配置。", this);
                return;
            }

            if (!HasMeaningfulChange(cachedValue, value))
            {
                return;
            }

            var result = command.SetResourceCurrent(actorId, resourceId, value, SourceModule);
            if (result == null || !result.Succeeded)
            {
                WarnOnce(ref warnedMissing, $"同步 TPC 当前资源到 Attribute 失败：ResourceId={resourceId}, Reason={result?.FailureReason}, Message={result?.Message}", this);
                return;
            }

            cachedValue = value;
        }

        private bool HasMeaningfulChange(float oldValue, float newValue)
        {
            return float.IsNaN(oldValue) || Mathf.Abs(oldValue - newValue) > valueEpsilon;
        }

        private void ResetCachedValues()
        {
            _lastHealth = float.NaN;
            _lastMaxHealth = float.NaN;
            _lastStamina = float.NaN;
            _lastMaxStamina = float.NaN;
        }

        private void WarnOnce(ref bool flag, string message, Object context)
        {
            if (!logWarnings || flag)
            {
                return;
            }

            Debug.LogWarning($"[TPCAttributeBridge] {message}", context);
            flag = true;
        }
    }
}
