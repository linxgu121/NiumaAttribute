using NiumaAttribute.Controller;
using NiumaAttribute.ViewData;
using UnityEngine;

namespace NiumaAttribute.Bridge
{
    /// <summary>
    /// 属性资源条桥接层。
    /// 面向 HP、MP、Stamina 等单个资源条，只查询一个资源，避免高频刷新时重建完整属性面板。
    /// </summary>
    public sealed class AttributeResourceBarBridge : MonoBehaviour
    {
        [Header("模块引用")]
        [Tooltip("属性模块根控制器。请拖入场景中的 NiumaAttributeController；为空时可按配置自动查找。")]
        [SerializeField] private NiumaAttributeController attributeController;

        [Tooltip("实现 IAttributeResourceBarReceiver 的 UI 组件。桥接层会把单个资源表现数据交给它显示。")]
        [SerializeField] private MonoBehaviour resourceBarReceiverProvider;

        [Header("自动查找")]
        [Tooltip("没有手动绑定属性控制器时，是否在场景中自动查找 NiumaAttributeController。正式场景建议手动绑定。")]
        [SerializeField] private bool autoFindAttributeController = true;

        [Header("资源绑定")]
        [Tooltip("当前显示的 ActorId。玩家可填 player；NPC、远端玩家或召唤物请填对应稳定 ActorId。")]
        [SerializeField] private string actorId = "player";

        [Tooltip("要显示的资源 ID。例如 health、mp、stamina。")]
        [SerializeField] private string resourceId = "health";

        [Header("刷新策略")]
        [Tooltip("启用桥接层时是否立即刷新一次资源条。")]
        [SerializeField] private bool refreshOnEnable = true;

        [Tooltip("是否在 LateUpdate 中自动刷新资源条。资源自然恢复期间建议开启。")]
        [SerializeField] private bool refreshInLateUpdate = true;

        [Tooltip("最小刷新间隔。0 表示每帧检查；资源条需要平滑变化时可以保持 0。")]
        [SerializeField] private float minRefreshInterval;

        [Tooltip("资源不存在或服务未就绪时，是否发送空数据给 UI 接收接口。")]
        [SerializeField] private bool notifyWhenMissing = true;

        [Header("日志")]
        [Tooltip("桥接层缺少必要引用或检测到 UI 刷新回流时是否打印警告。")]
        [SerializeField] private bool logWarnings = true;

        private IAttributeResourceBarReceiver _receiver;
        private ResourceValueViewData _lastResourceData;
        private long _observedRevision = -1L;
        private float _nextAllowedRefreshTime;
        private bool _isApplyingUpdate;
        private bool _refreshRequested;

        private void Reset()
        {
            ResolveReferences(false);
        }

        private void OnEnable()
        {
            ResolveReferences(true);
            _observedRevision = -1L;
            _isApplyingUpdate = false;
            _nextAllowedRefreshTime = 0f;

            if (refreshOnEnable)
            {
                RefreshResourceBar();
            }
        }

        private void OnDisable()
        {
            _isApplyingUpdate = false;
            _refreshRequested = false;
        }

        private void LateUpdate()
        {
            if (_refreshRequested)
            {
                _refreshRequested = false;
                RefreshResourceBar();
                return;
            }

            if (!refreshInLateUpdate || !EnsureController())
            {
                return;
            }

            if (minRefreshInterval > 0f && Time.unscaledTime < _nextAllowedRefreshTime)
            {
                return;
            }

            if (_observedRevision == attributeController.AttributeRevision)
            {
                return;
            }

            RefreshResourceBar();
        }

        /// <summary>
        /// 手动刷新资源条。
        /// 只读取单个资源，不重建完整属性面板。
        /// </summary>
        public void RefreshResourceBar()
        {
            if (!EnsureController())
            {
                ApplyMissingUpdate();
                return;
            }

            _nextAllowedRefreshTime = Time.unscaledTime + Mathf.Max(0f, minRefreshInterval);
            var revision = attributeController.AttributeRevision;
            var query = attributeController.AttributeQuery;
            ResourceValueViewData resourceData = null;
            if (query != null
                && !string.IsNullOrWhiteSpace(actorId)
                && !string.IsNullOrWhiteSpace(resourceId)
                && query.TryGetResource(actorId, resourceId, out var value))
            {
                resourceData = value;
            }

            _observedRevision = revision;
            if (resourceData == null)
            {
                ApplyMissingUpdate();
                return;
            }

            if (AreResourceDataSame(resourceData, _lastResourceData))
            {
                return;
            }

            ApplyRawUpdate(new AttributeResourceBarUpdate(revision, resourceData, _lastResourceData));
            _lastResourceData = resourceData;
        }

        /// <summary>
        /// 设置资源条绑定对象并请求下一帧刷新。
        /// </summary>
        public void SetActorId(string value)
        {
            if (!string.Equals(actorId, value, System.StringComparison.Ordinal))
            {
                _lastResourceData = null;
            }

            actorId = value;
            RequestRefresh();
        }

        /// <summary>
        /// 设置资源 ID 并请求下一帧刷新。
        /// </summary>
        public void SetResourceId(string value)
        {
            if (!string.Equals(resourceId, value, System.StringComparison.Ordinal))
            {
                _lastResourceData = null;
            }

            resourceId = value;
            RequestRefresh();
        }

        /// <summary>
        /// 一次性设置资源条上下文。
        /// </summary>
        public void SetContext(string newActorId, string newResourceId)
        {
            if (!string.Equals(actorId, newActorId, System.StringComparison.Ordinal)
                || !string.Equals(resourceId, newResourceId, System.StringComparison.Ordinal))
            {
                _lastResourceData = null;
            }

            actorId = newActorId;
            resourceId = newResourceId;
            RequestRefresh();
        }

        private void RequestRefresh()
        {
            _observedRevision = -1L;
            _refreshRequested = true;
        }

        private void ApplyMissingUpdate()
        {
            _observedRevision = attributeController != null ? attributeController.AttributeRevision : -1L;
            if (!notifyWhenMissing && _lastResourceData == null)
            {
                return;
            }

            ApplyRawUpdate(new AttributeResourceBarUpdate(_observedRevision, null, _lastResourceData));
            _lastResourceData = null;
        }

        private void ApplyRawUpdate(AttributeResourceBarUpdate update)
        {
            if (_receiver == null)
            {
                ResolveReceiver(true);
            }

            if (_receiver == null)
            {
                return;
            }

            if (_isApplyingUpdate)
            {
                _refreshRequested = true;
                if (logWarnings)
                {
                    Debug.LogWarning("[NiumaAttributeResourceBarBridge] 检测到资源条刷新回流，本次刷新已延后到下一帧。", this);
                }

                return;
            }

            var revisionBeforeApply = attributeController != null ? attributeController.AttributeRevision : update.Revision;
            try
            {
                _isApplyingUpdate = true;
                _receiver.ApplyResourceBarUpdate(update);
            }
            finally
            {
                _isApplyingUpdate = false;
            }

            if (attributeController != null && attributeController.AttributeRevision != revisionBeforeApply)
            {
                _observedRevision = -1L;
                _refreshRequested = true;
                if (logWarnings)
                {
                    Debug.LogWarning("[NiumaAttributeResourceBarBridge] UI 接收器回调中修改了属性数据，桥接层会在下一帧重新刷新。", this);
                }
            }
        }

        private bool EnsureController()
        {
            if (attributeController != null)
            {
                return true;
            }

            ResolveReferences(logWarnings);
            return attributeController != null;
        }

        private void ResolveReferences(bool logMissing)
        {
            if (autoFindAttributeController && attributeController == null)
            {
#if UNITY_2023_1_OR_NEWER
                attributeController = FindFirstObjectByType<NiumaAttributeController>();
#else
                attributeController = FindObjectOfType<NiumaAttributeController>();
#endif
            }

            ResolveReceiver(logMissing);

            if (logMissing && attributeController == null)
            {
                Debug.LogWarning("[NiumaAttributeResourceBarBridge] 未找到 NiumaAttributeController，请在 Inspector 中绑定。", this);
            }
        }

        private void ResolveReceiver(bool logMissing)
        {
            _receiver = resourceBarReceiverProvider as IAttributeResourceBarReceiver;
            if (logMissing && resourceBarReceiverProvider != null && _receiver == null)
            {
                Debug.LogWarning("[NiumaAttributeResourceBarBridge] resourceBarReceiverProvider 未实现 IAttributeResourceBarReceiver。", this);
            }
        }

        private static bool AreResourceDataSame(ResourceValueViewData left, ResourceValueViewData right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(left.ActorId, right.ActorId, System.StringComparison.Ordinal)
                   && string.Equals(left.ResourceId, right.ResourceId, System.StringComparison.Ordinal)
                   && string.Equals(left.DisplayName, right.DisplayName, System.StringComparison.Ordinal)
                   && Mathf.Abs(left.CurrentValue - right.CurrentValue) <= 0.0001f
                   && Mathf.Abs(left.MaxValue - right.MaxValue) <= 0.0001f
                   && Mathf.Abs(left.Percent - right.Percent) <= 0.0001f
                   && left.IsRecovering == right.IsRecovering
                   && Mathf.Abs(left.RecoveryPerSecond - right.RecoveryPerSecond) <= 0.0001f
                   && left.IsMissingDefinition == right.IsMissingDefinition;
        }
    }
}
