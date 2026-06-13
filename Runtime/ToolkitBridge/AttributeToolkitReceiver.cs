using NiumaUI.Toolkit;
using UnityEngine;

namespace NiumaAttribute.Bridge
{
    public sealed class AttributeToolkitReceiver : MonoBehaviour, IAttributeUIReceiver
    {
        [Header("Toolkit")]
        [SerializeField, Tooltip("拖核心场景 UIRoot/UIManager 上的 UIToolkitUIManager。")]
        private UIToolkitUIManager uiManager;
        [SerializeField, Tooltip("属性面板 ViewId。默认 AttributePanel，需要在 UIToolkitViewRegistrySO 注册同名 View。")]
        private string attributeViewId = "AttributePanel";
        [SerializeField, Tooltip("刷新失败时是否自动打开属性面板。")]
        private bool autoOpenView = true;
        [SerializeField, Tooltip("收到 Cleared 更新时是否关闭属性面板。关闭后会立即返回，不再重新打开。")]
        private bool closeOnCleared = true;
        [SerializeField, Tooltip("缺少 UIManager 或 View 时是否输出警告。")]
        private bool logWarnings = true;

        public void ApplyAttributeUpdate(AttributeUIUpdate update)
        {
            if (update.UpdateType == AttributeUIUpdateType.Cleared && closeOnCleared && uiManager != null)
            {
                uiManager.CloseView(attributeViewId);
                return;
            }

            RefreshOrOpen(update);
        }

        private void RefreshOrOpen(AttributeUIUpdate update)
        {
            if (!EnsureUIManager())
                return;

            var refreshed = uiManager.RefreshView(attributeViewId, update);
            if (!refreshed && autoOpenView)
                refreshed = uiManager.OpenView(attributeViewId, update);

            if (!refreshed)
                Warn($"没有刷新到属性 Toolkit View：ViewId={attributeViewId}。请检查 Registry 和 BindingProvider。");
        }

        private bool EnsureUIManager()
        {
            if (uiManager == null)
                uiManager = FindSceneObject<UIToolkitUIManager>();

            if (uiManager != null)
                return true;

            Warn("未绑定 UIToolkitUIManager，属性 Toolkit 面板无法刷新。");
            return false;
        }

        private void Warn(string message)
        {
            if (logWarnings && !string.IsNullOrWhiteSpace(message))
                UnityEngine.Debug.LogWarning($"[AttributeToolkitReceiver] {message}", this);
        }

        private static T FindSceneObject<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<T>();
#else
            return FindObjectOfType<T>();
#endif
        }
    }

    public sealed class AttributeResourceBarToolkitReceiver : MonoBehaviour, IAttributeResourceBarReceiver
    {
        [Header("Toolkit")]
        [SerializeField, Tooltip("拖核心场景 UIRoot/UIManager 上的 UIToolkitUIManager。")]
        private UIToolkitUIManager uiManager;
        [SerializeField, Tooltip("资源条 ViewId。默认 AttributeResourceBar，需要在 UIToolkitViewRegistrySO 注册同名 View。")]
        private string resourceBarViewId = "AttributeResourceBar";
        [SerializeField, Tooltip("刷新失败时是否自动打开资源条。")]
        private bool autoOpenView = true;
        [SerializeField, Tooltip("资源数据为空时是否关闭资源条。关闭后会立即返回，不再重新打开。")]
        private bool closeWhenMissing = false;
        [SerializeField, Tooltip("缺少 UIManager 或 View 时是否输出警告。")]
        private bool logWarnings = true;

        public void ApplyResourceBarUpdate(AttributeResourceBarUpdate update)
        {
            if (!update.HasResourceData && closeWhenMissing && uiManager != null)
            {
                uiManager.CloseView(resourceBarViewId);
                return;
            }

            RefreshOrOpen(update);
        }

        private void RefreshOrOpen(AttributeResourceBarUpdate update)
        {
            if (!EnsureUIManager())
                return;

            var refreshed = uiManager.RefreshView(resourceBarViewId, update);
            if (!refreshed && autoOpenView)
                refreshed = uiManager.OpenView(resourceBarViewId, update);

            if (!refreshed)
                Warn($"没有刷新到属性资源条 Toolkit View：ViewId={resourceBarViewId}。请检查 Registry 和 BindingProvider。");
        }

        private bool EnsureUIManager()
        {
            if (uiManager == null)
                uiManager = FindSceneObject<UIToolkitUIManager>();

            if (uiManager != null)
                return true;

            Warn("未绑定 UIToolkitUIManager，属性资源条无法刷新。");
            return false;
        }

        private void Warn(string message)
        {
            if (logWarnings && !string.IsNullOrWhiteSpace(message))
                UnityEngine.Debug.LogWarning($"[AttributeResourceBarToolkitReceiver] {message}", this);
        }

        private static T FindSceneObject<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<T>();
#else
            return FindObjectOfType<T>();
#endif
        }
    }
}