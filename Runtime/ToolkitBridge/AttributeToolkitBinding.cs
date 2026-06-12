using System;
using NiumaAttribute.ViewData;
using NiumaUI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace NiumaAttribute.Bridge
{
    public sealed class AttributeToolkitBindingProvider : MonoBehaviour, IToolkitViewBindingProvider
    {
        [SerializeField, Tooltip("BindingProviderId，默认 AttributePanel。需要和 Registry 一致。")]
        private string providerId = "AttributePanel";
        [SerializeField] private string titleLabelName = "TitleText";
        [SerializeField] private string statusLabelName = "StatusText";
        [SerializeField] private string listRootName = "ListRoot";
        [SerializeField] private string detailLabelName = "DetailText";
        [SerializeField] private string resultLabelName = "ResultText";
        [SerializeField] private string emptyRootName = "EmptyRoot";
        [SerializeField] private int maxRows = 80;
        [SerializeField] private string rowClass = "niuma-attribute-row";

        public string ProviderId => string.IsNullOrWhiteSpace(providerId) ? "AttributePanel" : providerId.Trim();
        public IToolkitViewBinding CreateBinding() => new AttributeToolkitBinding(titleLabelName, statusLabelName, listRootName, detailLabelName, resultLabelName, emptyRootName, maxRows, rowClass);
    }

    public sealed class AttributeResourceBarToolkitBindingProvider : MonoBehaviour, IToolkitViewBindingProvider
    {
        [SerializeField, Tooltip("BindingProviderId，默认 AttributeResourceBar。需要和 Registry 一致。")]
        private string providerId = "AttributeResourceBar";
        [SerializeField] private string titleLabelName = "TitleText";
        [SerializeField] private string valueLabelName = "ValueText";
        [SerializeField] private string fillElementName = "FillElement";
        [SerializeField] private string emptyRootName = "EmptyRoot";

        public string ProviderId => string.IsNullOrWhiteSpace(providerId) ? "AttributeResourceBar" : providerId.Trim();
        public IToolkitViewBinding CreateBinding() => new AttributeResourceBarToolkitBinding(titleLabelName, valueLabelName, fillElementName, emptyRootName);
    }

    public sealed class AttributeToolkitBinding : ToolkitViewBindingBase
    {
        private readonly string _titleName, _statusName, _listName, _detailName, _resultName, _emptyName, _rowClass;
        private readonly int _maxRows;
        private Label _title, _status, _detail, _result;
        private VisualElement _list, _empty;

        public AttributeToolkitBinding(string titleName, string statusName, string listName, string detailName, string resultName, string emptyName, int maxRows, string rowClass)
        {
            _titleName = titleName; _statusName = statusName; _listName = listName; _detailName = detailName; _resultName = resultName; _emptyName = emptyName;
            _maxRows = Mathf.Max(1, maxRows);
            _rowClass = string.IsNullOrWhiteSpace(rowClass) ? "niuma-attribute-row" : rowClass.Trim();
        }

        protected override void OnInitialize()
        {
            _title = QL(_titleName); _status = QL(_statusName); _list = QE(_listName); _detail = QL(_detailName); _result = QL(_resultName); _empty = QE(_emptyName);
            Apply(null, AttributeUIUpdateType.Cleared, 0);
        }

        protected override void OnRefresh(object viewData)
        {
            if (viewData is AttributeUIUpdate update) Apply(update.PanelData, update.UpdateType, update.Revision);
            else Apply(null, AttributeUIUpdateType.Cleared, 0);
        }

        protected override void OnClose() => Apply(null, AttributeUIUpdateType.Cleared, 0);

        private void Apply(AttributePanelViewData panel, AttributeUIUpdateType updateType, long revision)
        {
            Clear();
            var attrs = panel?.Attributes ?? Array.Empty<AttributeValueViewData>();
            var resources = panel?.Resources ?? Array.Empty<ResourceValueViewData>();
            Set(_title, "角色数值");
            SetVisible(_empty, panel == null || (attrs.Length == 0 && resources.Length == 0));

            if (panel == null)
            {
                Set(_status, $"状态：{updateType}");
                Set(_detail, "暂无属性数据。");
                Set(_result, string.Empty);
                return;
            }

            Set(_status, $"Actor {Text(panel.ActorId, "未知")} | Revision {panel.Revision} | 属性 {attrs.Length} | 资源 {resources.Length}");
            Set(_detail, "资源和属性列表由 AttributeService 计算，UI 只负责显示。");
            Set(_result, string.Empty);

            var rows = 0;
            for (var i = 0; i < resources.Length && rows < _maxRows; i++)
            {
                var r = resources[i];
                if (r == null) continue;
                Add($"[资源] {Text(r.DisplayName, r.ResourceId)} {r.CurrentValue:0.##}/{r.MaxValue:0.##} ({r.Percent:P0}){(r.IsRecovering ? $" +{r.RecoveryPerSecond:0.##}/s" : string.Empty)}{(r.IsMissingDefinition ? " | 缺失定义" : string.Empty)}");
                rows++;
            }

            for (var i = 0; i < attrs.Length && rows < _maxRows; i++)
            {
                var a = attrs[i];
                if (a == null) continue;
                Add($"[属性] {Text(a.DisplayName, a.AttributeId)} = {a.FinalValue:0.##} (基础 {a.BaseValue:0.##}, 修正 {a.ModifierDelta:+0.##;-0.##;0}) | {a.Kind}/{a.DominantLayer}{(a.IsMissingDefinition ? " | 缺失定义" : string.Empty)}");
                rows++;
            }
        }

        private Label QL(string name) => string.IsNullOrWhiteSpace(name) ? null : Query<Label>(name.Trim());
        private VisualElement QE(string name) => string.IsNullOrWhiteSpace(name) ? null : Root?.Q<VisualElement>(name.Trim());
        private void Clear() { if (_list != null) _list.Clear(); }
        private void Add(string text) { if (_list == null) return; var row = new Label(text ?? string.Empty); row.AddToClassList(_rowClass); _list.Add(row); }
        private static void Set(Label label, string text) { if (label != null) label.text = text ?? string.Empty; }
        private static void SetVisible(VisualElement element, bool visible) { if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None; }
        private static string Text(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
    }

    public sealed class AttributeResourceBarToolkitBinding : ToolkitViewBindingBase
    {
        private readonly string _titleName, _valueName, _fillName, _emptyName;
        private Label _title, _value;
        private VisualElement _fill, _empty;

        public AttributeResourceBarToolkitBinding(string titleName, string valueName, string fillName, string emptyName)
        {
            _titleName = titleName; _valueName = valueName; _fillName = fillName; _emptyName = emptyName;
        }

        protected override void OnInitialize()
        {
            _title = QL(_titleName); _value = QL(_valueName); _fill = QE(_fillName); _empty = QE(_emptyName);
            Apply(null);
        }

        protected override void OnRefresh(object viewData)
        {
            if (viewData is AttributeResourceBarUpdate update) Apply(update.ResourceData);
            else Apply(null);
        }

        protected override void OnClose() => Apply(null);

        private void Apply(ResourceValueViewData data)
        {
            SetVisible(_empty, data == null);
            if (data == null)
            {
                Set(_title, "资源");
                Set(_value, string.Empty);
                SetFill(0f);
                return;
            }

            Set(_title, Text(data.DisplayName, data.ResourceId));
            Set(_value, $"{data.CurrentValue:0.##}/{data.MaxValue:0.##}");
            SetFill(Mathf.Clamp01(data.Percent));
        }

        private void SetFill(float percent)
        {
            if (_fill != null) _fill.style.width = Length.Percent(percent * 100f);
        }

        private Label QL(string name) => string.IsNullOrWhiteSpace(name) ? null : Query<Label>(name.Trim());
        private VisualElement QE(string name) => string.IsNullOrWhiteSpace(name) ? null : Root?.Q<VisualElement>(name.Trim());
        private static void Set(Label label, string text) { if (label != null) label.text = text ?? string.Empty; }
        private static void SetVisible(VisualElement element, bool visible) { if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None; }
        private static string Text(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
    }
}
