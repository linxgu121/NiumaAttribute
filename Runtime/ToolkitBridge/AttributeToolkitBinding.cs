using System;
using System.Collections.Generic;
using NiumaAttribute.ViewData;
using NiumaUI.Toolkit;
using NiumaUI.Toolkit.Common;
using UnityEngine;
using UnityEngine.UIElements;

namespace NiumaAttribute.Bridge
{
    public sealed class AttributeToolkitBindingProvider : ToolkitViewBindingProviderBase
    {
        [Header("元素名称")]
        [SerializeField, Tooltip("标题 Label 的 name。默认 TitleText。")]
        private string titleLabelName = "TitleText";
        [SerializeField, Tooltip("状态 Label 的 name。默认 StatusText。")]
        private string statusLabelName = "StatusText";
        [SerializeField, Tooltip("属性与资源列表 ListView 的 name。默认 ListRoot。")]
        private string listViewName = "ListRoot";
        [SerializeField, Tooltip("详情 Label 的 name。用于显示说明文本。")]
        private string detailLabelName = "DetailText";
        [SerializeField, Tooltip("结果 Label 的 name。属性面板通常留空。")]
        private string resultLabelName = "ResultText";
        [SerializeField, Tooltip("空状态节点的 name。没有属性数据时显示。")]
        private string emptyRootName = "EmptyRoot";

        [Header("列表")]
        [SerializeField, Tooltip("最多显示多少行，避免调试面板过重。")]
        private int maxRows = 80;
        [SerializeField, Tooltip("列表行 USS class。")]
        private string rowClass = "niuma-attribute-row";
        [SerializeField, Tooltip("选中行 USS class。属性面板第一版不使用选中态，保留给后续扩展。")]
        private string selectedRowClass = "is-selected";
        [SerializeField, Tooltip("禁用行 USS class。")]
        private string disabledRowClass = "is-disabled";

        protected override string DefaultProviderId => "AttributePanel";

        public override IToolkitViewBinding CreateBinding()
        {
            return new AttributeToolkitBinding(
                titleLabelName,
                statusLabelName,
                listViewName,
                detailLabelName,
                resultLabelName,
                emptyRootName,
                maxRows,
                rowClass,
                selectedRowClass,
                disabledRowClass);
        }
    }

    public sealed class AttributeResourceBarToolkitBindingProvider : ToolkitViewBindingProviderBase
    {
        [Header("元素名称")]
        [SerializeField, Tooltip("资源名称 Label 的 name。默认 TitleText。")]
        private string titleLabelName = "TitleText";
        [SerializeField, Tooltip("资源数值 Label 的 name。默认 ValueText。")]
        private string valueLabelName = "ValueText";
        [SerializeField, Tooltip("填充条 VisualElement 的 name。默认 FillElement。")]
        private string fillElementName = "FillElement";
        [SerializeField, Tooltip("空状态节点的 name。没有资源数据时显示。")]
        private string emptyRootName = "EmptyRoot";

        protected override string DefaultProviderId => "AttributeResourceBar";

        public override IToolkitViewBinding CreateBinding()
        {
            return new AttributeResourceBarToolkitBinding(titleLabelName, valueLabelName, fillElementName, emptyRootName);
        }
    }

    public sealed class AttributeToolkitViewModel : UIPanelViewModelBase
    {
        public readonly List<ToolkitTextRowData> Rows = new List<ToolkitTextRowData>();
        public AttributePanelViewData Panel { get; private set; }
        public AttributeUIUpdateType UpdateType { get; private set; }
        public long Revision { get; private set; }

        public void Apply(AttributeUIUpdate update, int maxRows)
        {
            Panel = update.PanelData;
            UpdateType = update.UpdateType;
            Revision = update.Revision;
            SetContext(Panel?.ActorId);
            RebuildRows(maxRows);
            MarkDirty();
        }

        protected override void OnClear(UIViewModelClearReason reason)
        {
            Panel = null;
            UpdateType = AttributeUIUpdateType.Cleared;
            Revision = 0;
            Rows.Clear();
        }

        private void RebuildRows(int maxRows)
        {
            Rows.Clear();
            if (Panel == null)
                return;

            var rowsLeft = Math.Max(1, maxRows);
            var resources = Panel.Resources ?? Array.Empty<ResourceValueViewData>();
            for (var i = 0; i < resources.Length && rowsLeft > 0; i++)
            {
                var resource = resources[i];
                if (resource == null)
                    continue;

                var id = string.IsNullOrWhiteSpace(resource.ResourceId) ? $"resource:{i}" : resource.ResourceId.Trim();
                var recovery = resource.IsRecovering ? $" +{resource.RecoveryPerSecond:0.##}/s" : string.Empty;
                var missing = resource.IsMissingDefinition ? " | 缺失定义" : string.Empty;
                Rows.Add(new ToolkitTextRowData(id, $"[资源] {Text(resource.DisplayName, resource.ResourceId)} {resource.CurrentValue:0.##}/{resource.MaxValue:0.##} ({resource.Percent:P0}){recovery}{missing}"));
                rowsLeft--;
            }

            var attributes = Panel.Attributes ?? Array.Empty<AttributeValueViewData>();
            for (var i = 0; i < attributes.Length && rowsLeft > 0; i++)
            {
                var attribute = attributes[i];
                if (attribute == null)
                    continue;

                var id = string.IsNullOrWhiteSpace(attribute.AttributeId) ? $"attribute:{i}" : attribute.AttributeId.Trim();
                var missing = attribute.IsMissingDefinition ? " | 缺失定义" : string.Empty;
                Rows.Add(new ToolkitTextRowData(id, $"[属性] {Text(attribute.DisplayName, attribute.AttributeId)} = {attribute.FinalValue:0.##} (基础 {attribute.BaseValue:0.##}, 修正 {attribute.ModifierDelta:+0.##;-0.##;0}) | {attribute.Kind}/{attribute.DominantLayer}{missing}"));
                rowsLeft--;
            }
        }

        private static string Text(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
        }
    }

    public sealed class AttributeResourceBarToolkitViewModel : UIPanelViewModelBase
    {
        public ResourceValueViewData Resource { get; private set; }
        public long Revision { get; private set; }

        public void Apply(AttributeResourceBarUpdate update)
        {
            Resource = update.ResourceData;
            Revision = update.Revision;
            SetContext(Resource?.ActorId);
            MarkDirty();
        }

        protected override void OnClear(UIViewModelClearReason reason)
        {
            Resource = null;
            Revision = 0;
        }
    }

    public sealed class AttributeToolkitBinding : ToolkitViewBindingBase<AttributeUIUpdate, AttributeToolkitViewModel>
    {
        private readonly string _titleName;
        private readonly string _statusName;
        private readonly string _listName;
        private readonly string _detailName;
        private readonly string _resultName;
        private readonly string _emptyName;
        private readonly int _maxRows;
        private readonly string _rowClass;
        private readonly string _selectedClass;
        private readonly string _disabledClass;
        private readonly ToolkitListBinding<ToolkitTextRowData> _listBinding = new ToolkitListBinding<ToolkitTextRowData>();
        private Label _title;
        private Label _status;
        private Label _detail;
        private Label _result;

        public AttributeToolkitBinding(
            string titleName,
            string statusName,
            string listName,
            string detailName,
            string resultName,
            string emptyName,
            int maxRows,
            string rowClass,
            string selectedClass,
            string disabledClass)
        {
            _titleName = titleName;
            _statusName = statusName;
            _listName = listName;
            _detailName = detailName;
            _resultName = resultName;
            _emptyName = emptyName;
            _maxRows = Mathf.Max(1, maxRows);
            _rowClass = string.IsNullOrWhiteSpace(rowClass) ? "niuma-attribute-row" : rowClass.Trim();
            _selectedClass = selectedClass;
            _disabledClass = disabledClass;
        }

        protected override void OnInitializeTyped()
        {
            _title = QLabel(_titleName);
            _status = QLabel(_statusName);
            _detail = QLabel(_detailName);
            _result = QLabel(_resultName);
            _listBinding.Bind(Root, _listName, new ToolkitTextRowItemBinder(_rowClass, _selectedClass, _disabledClass, null), _emptyName);
            ApplyVisualState(ViewModel);
        }

        protected override void OnRefreshTyped(AttributeUIUpdate viewData, AttributeToolkitViewModel viewModel)
        {
            viewModel.Apply(viewData, _maxRows);
            ApplyVisualState(viewModel);
        }

        protected override void OnClearTyped(UIViewModelClearReason reason)
        {
            _listBinding.Clear();
            ApplyVisualState(ViewModel);
        }

        protected override void OnDisposeTyped()
        {
            _listBinding.Dispose();
        }

        private void ApplyVisualState(AttributeToolkitViewModel viewModel)
        {
            var panel = viewModel?.Panel;
            var rows = viewModel?.Rows ?? new List<ToolkitTextRowData>();
            SetText(_title, "角色数值");
            SetText(_status, panel == null
                ? $"状态：{viewModel?.UpdateType ?? AttributeUIUpdateType.Cleared}"
                : $"Actor {Text(panel.ActorId, "未知")} | Revision {panel.Revision} | 属性 {panel.Attributes?.Length ?? 0} | 资源 {panel.Resources?.Length ?? 0}");
            SetText(_detail, panel == null ? "暂无属性数据。" : "资源和属性列表由 AttributeService 计算，UI 只负责显示。");
            SetText(_result, string.Empty);
            _listBinding.ReplaceAll(rows);
        }

        private static string Text(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
        }
    }

    public sealed class AttributeResourceBarToolkitBinding : ToolkitViewBindingBase<AttributeResourceBarUpdate, AttributeResourceBarToolkitViewModel>
    {
        private readonly string _titleName;
        private readonly string _valueName;
        private readonly string _fillName;
        private readonly string _emptyName;
        private Label _title;
        private Label _value;
        private VisualElement _fill;
        private VisualElement _empty;

        public AttributeResourceBarToolkitBinding(string titleName, string valueName, string fillName, string emptyName)
        {
            _titleName = titleName;
            _valueName = valueName;
            _fillName = fillName;
            _emptyName = emptyName;
        }

        protected override void OnInitializeTyped()
        {
            _title = QLabel(_titleName);
            _value = QLabel(_valueName);
            _fill = Query<VisualElement>(_fillName);
            _empty = Query<VisualElement>(_emptyName);
            ApplyVisualState(ViewModel);
        }

        protected override void OnRefreshTyped(AttributeResourceBarUpdate viewData, AttributeResourceBarToolkitViewModel viewModel)
        {
            viewModel.Apply(viewData);
            ApplyVisualState(viewModel);
        }

        protected override void OnClearTyped(UIViewModelClearReason reason)
        {
            ApplyVisualState(ViewModel);
        }

        private void ApplyVisualState(AttributeResourceBarToolkitViewModel viewModel)
        {
            var data = viewModel?.Resource;
            SetElementVisible(_empty, data == null);
            if (data == null)
            {
                SetText(_title, "资源");
                SetText(_value, string.Empty);
                SetFill(0f);
                return;
            }

            SetText(_title, Text(data.DisplayName, data.ResourceId));
            SetText(_value, $"{data.CurrentValue:0.##}/{data.MaxValue:0.##}");
            SetFill(Mathf.Clamp01(data.Percent));
        }

        private void SetFill(float percent)
        {
            if (_fill != null)
                _fill.style.width = Length.Percent(percent * 100f);
        }

        private static string Text(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
        }
    }
}