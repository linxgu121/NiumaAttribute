using System;
using NiumaAttribute.Controller;
using NiumaAttribute.ViewData;
using UnityEngine;

namespace NiumaAttribute.Bridge
{
    /// <summary>
    /// 属性模块到 UI 模块的数据驱动桥接层。
    /// 桥接层按属性修订号拉取指定 Actor 的表现数据，不订阅事件，也不直接依赖具体 UI 框架。
    /// </summary>
    public sealed class AttributeUIViewBridge : MonoBehaviour
    {
        [Header("模块引用")]
        [Tooltip("属性模块根控制器。请拖入场景中的 NiumaAttributeController；为空时可按配置自动查找。")]
        [SerializeField] private NiumaAttributeController attributeController;

        [Tooltip("属性面板 UI 脚本。拖团队制作的 Attribute 面板脚本；该脚本负责显示属性列表和资源概览。当前模块未内置正式面板，未制作 UI 时可留空。")]
        [SerializeField] private MonoBehaviour attributeUIReceiverProvider;

        [Header("自动查找")]
        [Tooltip("没有手动绑定属性控制器时，是否在场景中自动查找 NiumaAttributeController。正式场景建议手动绑定。")]
        [SerializeField] private bool autoFindAttributeController = true;

        [Header("刷新策略")]
        [Tooltip("启用桥接层时是否立即刷新一次属性面板。")]
        [SerializeField] private bool refreshOnEnable = true;

        [Tooltip("是否在 LateUpdate 中按属性版本号自动刷新 UI。关闭后需要外部手动调用 RefreshAttributePanel。")]
        [SerializeField] private bool refreshInLateUpdate = true;

        [Tooltip("没有属性服务、Actor 不存在或没有可显示数据时，是否发送 Cleared 更新给 UI 接收接口。")]
        [SerializeField] private bool notifyWhenCleared = true;

        [Header("显示对象")]
        [Tooltip("当前显示的 ActorId。玩家可填 player；NPC、远端玩家或召唤物请填对应稳定 ActorId。")]
        [SerializeField] private string actorId = "player";

        [Header("日志")]
        [Tooltip("桥接层缺少必要引用或检测到 UI 刷新回流时是否打印警告。")]
        [SerializeField] private bool logWarnings = true;

        private IAttributeUIReceiver _receiver;
        private long _observedRevision = -1L;
        private AttributePanelViewData _lastPanelData;
        private bool _hadPanelData;
        private bool _isApplyingUpdate;
        private bool _refreshRequested;
        private long _lastBuildFailureRevision = long.MinValue;

        private void Reset()
        {
            ResolveReferences(false);
        }

        private void OnEnable()
        {
            ResolveReferences(true);
            _observedRevision = -1L;
            _isApplyingUpdate = false;

            if (refreshOnEnable)
            {
                RefreshAttributePanel();
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
                RefreshAttributePanel();
                return;
            }

            if (!refreshInLateUpdate || !EnsureController())
            {
                return;
            }

            if (_observedRevision == attributeController.AttributeRevision)
            {
                return;
            }

            RefreshAttributePanel();
        }

        /// <summary>
        /// 手动刷新属性面板。
        /// 只读取属性状态，不修改 Actor 数据。
        /// </summary>
        public void RefreshAttributePanel()
        {
            if (!EnsureController())
            {
                ApplyClearUpdate();
                return;
            }

            var targetRevision = attributeController.AttributeRevision;
            AttributePanelViewData panelData;
            try
            {
                panelData = BuildPanelViewData(targetRevision);
            }
            catch (Exception exception)
            {
                _observedRevision = -1L;
                if (logWarnings && _lastBuildFailureRevision != targetRevision)
                {
                    Debug.LogError($"[NiumaAttributeUIBridge] 构建属性 UI 表现数据失败，桥接层会在下一次刷新时重试。Revision={targetRevision}, Error={exception.Message}", this);
                }

                _lastBuildFailureRevision = targetRevision;
                return;
            }

            _lastBuildFailureRevision = long.MinValue;
            _observedRevision = targetRevision;
            if (panelData == null || (panelData.Attributes.Length == 0 && panelData.Resources.Length == 0))
            {
                ApplyClearUpdate();
                return;
            }

            _hadPanelData = true;
            ApplyRawUpdate(new AttributeUIUpdate(
                AttributeUIUpdateType.Refresh,
                _observedRevision,
                panelData,
                _lastPanelData));
            _lastPanelData = panelData;
        }

        /// <summary>
        /// 设置当前显示 Actor 并请求下一帧刷新。
        /// </summary>
        public void SetActorId(string value)
        {
            if (!string.Equals(actorId, value, StringComparison.Ordinal))
            {
                _lastPanelData = null;
                _hadPanelData = false;
            }

            actorId = value;
            RequestRefresh();
        }

        /// <summary>
        /// 清空当前显示 Actor 并请求下一帧刷新。
        /// </summary>
        public void ClearActor()
        {
            SetActorId(null);
        }

        private AttributePanelViewData BuildPanelViewData(long revision)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                return null;
            }

            var query = attributeController.AttributeQuery;
            if (query == null || !query.HasActor(actorId))
            {
                return null;
            }

            return new AttributePanelViewData
            {
                ActorId = actorId,
                Revision = revision,
                Attributes = query.GetAllAttributes(actorId),
                Resources = query.GetAllResources(actorId)
            };
        }

        private void RequestRefresh()
        {
            _observedRevision = -1L;
            _refreshRequested = true;
        }

        private void ApplyClearUpdate()
        {
            _observedRevision = attributeController != null ? attributeController.AttributeRevision : -1L;
            if (!notifyWhenCleared && !_hadPanelData)
            {
                return;
            }

            ApplyRawUpdate(new AttributeUIUpdate(
                AttributeUIUpdateType.Cleared,
                _observedRevision,
                null,
                _lastPanelData));
            _lastPanelData = null;
            _hadPanelData = false;
        }

        private void ApplyRawUpdate(AttributeUIUpdate update)
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
                    Debug.LogWarning("[NiumaAttributeUIBridge] 检测到 UI 刷新回流，本次刷新已延后到下一帧。", this);
                }

                return;
            }

            var revisionBeforeApply = attributeController != null ? attributeController.AttributeRevision : update.Revision;
            try
            {
                _isApplyingUpdate = true;
                _receiver.ApplyAttributeUpdate(update);
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
                    Debug.LogWarning("[NiumaAttributeUIBridge] UI 接收器回调中修改了属性数据，桥接层会在下一帧重新刷新。", this);
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
                Debug.LogWarning("[NiumaAttributeUIBridge] 未找到 NiumaAttributeController，请在 Inspector 中绑定。", this);
            }
        }

        private void ResolveReceiver(bool logMissing)
        {
            _receiver = attributeUIReceiverProvider as IAttributeUIReceiver;
            if (logMissing && attributeUIReceiverProvider != null && _receiver == null)
            {
                Debug.LogWarning("[NiumaAttributeUIBridge] Attribute UI Receiver 绑定的不是属性面板脚本，请拖团队制作的 Attribute 面板脚本。", this);
            }
        }
    }
}
