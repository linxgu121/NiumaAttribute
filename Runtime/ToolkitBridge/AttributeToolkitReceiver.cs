using NiumaUI.Toolkit;
using UnityEngine;

namespace NiumaAttribute.Bridge
{
    public sealed class AttributeToolkitReceiver : MonoBehaviour, IAttributeUIReceiver
    {
        [Header("Toolkit")]
        [Tooltip("拖核心场景 UIRoot/UIManager 上的 UIToolkitUIManager。")]
        [SerializeField] private UIToolkitUIManager uiManager;
        [Tooltip("属性面板 ViewId。默认 AttributePanel，需要在 UIToolkitViewRegistrySO 注册同名 View。")]
        [SerializeField] private string attributeViewId = "AttributePanel";
        [SerializeField] private bool autoOpenView = true;
        [SerializeField] private bool closeOnCleared = true;
        [SerializeField] private bool logWarnings = true;

        public void ApplyAttributeUpdate(AttributeUIUpdate update)
        {
            if (update.UpdateType == AttributeUIUpdateType.Cleared && closeOnCleared && uiManager != null)
                uiManager.CloseView(attributeViewId);
            RefreshOrOpen(update);
        }

        private void RefreshOrOpen(AttributeUIUpdate update)
        {
            if (!EnsureUIManager()) return;
            var refreshed = uiManager.RefreshView(attributeViewId, update);
            if (!refreshed && autoOpenView) refreshed = uiManager.OpenView(attributeViewId, update);
            if (!refreshed) Warn($"没有刷新到属性 Toolkit View：ViewId={attributeViewId}。请检查 Registry 和 BindingProvider。");
        }

        private bool EnsureUIManager()
        {
            if (uiManager == null) uiManager = FindSceneObject<UIToolkitUIManager>();
            if (uiManager != null) return true;
            Warn("未绑定 UIToolkitUIManager，属性 Toolkit 面板无法刷新。");
            return false;
        }

        private void Warn(string message)
        {
            if (logWarnings && !string.IsNullOrWhiteSpace(message)) UnityEngine.Debug.LogWarning($"[AttributeToolkitReceiver] {message}", this);
        }

        private static T FindSceneObject<T>() where T : Object
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
        [Tooltip("拖核心场景 UIRoot/UIManager 上的 UIToolkitUIManager。")]
        [SerializeField] private UIToolkitUIManager uiManager;
        [Tooltip("资源条 ViewId。默认 AttributeResourceBar，需要在 UIToolkitViewRegistrySO 注册同名 View。")]
        [SerializeField] private string resourceBarViewId = "AttributeResourceBar";
        [SerializeField] private bool autoOpenView = true;
        [SerializeField] private bool closeWhenMissing = false;
        [SerializeField] private bool logWarnings = true;

        public void ApplyResourceBarUpdate(AttributeResourceBarUpdate update)
        {
            if (!update.HasResourceData && closeWhenMissing && uiManager != null)
                uiManager.CloseView(resourceBarViewId);
            RefreshOrOpen(update);
        }

        private void RefreshOrOpen(AttributeResourceBarUpdate update)
        {
            if (!EnsureUIManager()) return;
            var refreshed = uiManager.RefreshView(resourceBarViewId, update);
            if (!refreshed && autoOpenView) refreshed = uiManager.OpenView(resourceBarViewId, update);
            if (!refreshed) Warn($"没有刷新到属性资源条 Toolkit View：ViewId={resourceBarViewId}。请检查 Registry 和 BindingProvider。");
        }

        private bool EnsureUIManager()
        {
            if (uiManager == null) uiManager = FindSceneObject<UIToolkitUIManager>();
            if (uiManager != null) return true;
            Warn("未绑定 UIToolkitUIManager，属性资源条无法刷新。");
            return false;
        }

        private void Warn(string message)
        {
            if (logWarnings && !string.IsNullOrWhiteSpace(message)) UnityEngine.Debug.LogWarning($"[AttributeResourceBarToolkitReceiver] {message}", this);
        }

        private static T FindSceneObject<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<T>();
#else
            return FindObjectOfType<T>();
#endif
        }
    }
}
