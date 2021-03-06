using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace AssetStoreTools.Uploader.UIElements
{
    internal class PackageGroup : VisualElement
    {
        // Category Data
        private string GroupName { get; }
        private readonly List<PackageView> _packages;
        
        // Visual Elements
        private Button _groupExpanderBox;
        private VisualElement _groupContent;
        
        private Label _expanderLabel;
        private Label _groupLabel;
        
        // Other
        private PackageView _expandedPackageView;
        
        private bool _expanded;
        private bool? _expandingOverriden;

        // Actions
        public Action<float> OnSliderChange;

        public PackageGroup(string groupName, bool createExpanded)
        {
            GroupName = groupName;
            AddToClassList("package-group");
            
            _packages = new List<PackageView>();
            _expanded = createExpanded;
            
            SetupSingleGroupElement();
            HandleExpanding();
        }

        public void AddPackage(PackageView packageView)
        {
            _packages.Add(packageView);
            _groupContent.Add(packageView);

            UpdateGroupLabel();
            packageView.OnPackageSelection = HandlePackageSelection;
            packageView.ShowFunctions(false);
        }

        public void SearchFilter(string filter)
        {
            var foundPackageCount = 0;
            foreach(var p in _packages)
            {
                if (p.SearchableText.Contains(filter))
                {
                    foundPackageCount++;
                    p.style.display = DisplayStyle.Flex;
                    _groupContent.style.display = DisplayStyle.Flex;
                }
                else
                    p.style.display = DisplayStyle.None;
            }

            if (string.IsNullOrEmpty(filter))
            {
                _expandingOverriden = null;
                
                UpdateGroupLabel();
                SetEnabled(true);
                HandleExpanding();
            }
            else
            {
                OverwriteGroupLabel($"{GroupName} ({foundPackageCount} found)");
                SetEnabled(foundPackageCount > 0);
                HandleExpanding(foundPackageCount > 0);
            }
        }

        private void SetupSingleGroupElement()
        {
            _groupExpanderBox = new Button();
            _groupExpanderBox.AddToClassList("group-expander-box");
            
            _expanderLabel = new Label { name = "ExpanderLabel", text = "►" };
            _expanderLabel.AddToClassList("expander");

            _groupLabel = new Label {text = $"{GroupName} ({_packages.Count})"};
            _groupLabel.AddToClassList("group-label");
            
            _groupExpanderBox.Add(_expanderLabel);
            _groupExpanderBox.Add(_groupLabel);

            _groupContent = new VisualElement {name = "GroupContentBox"};
            _groupContent.AddToClassList("group-content-box");

            _groupExpanderBox.clicked += () =>
            {
                if (_expandingOverriden == null)
                    _expanded = !_expanded;
                else
                    _expandingOverriden = !_expandingOverriden;

                HandleExpanding();
            };

            var groupSeparator = new VisualElement {name = "GroupSeparator"};
            groupSeparator.AddToClassList("group-separator");

            if (GroupName.ToLower() != "draft")
            {
                _groupLabel.SetEnabled(false);
                _groupContent.AddToClassList("unity-disabled");
                groupSeparator.style.display = DisplayStyle.Flex;
            }

            Add(_groupExpanderBox);
            Add(_groupContent);
            Add(groupSeparator);
        }

        private void HandleExpanding(bool? overrideExpanding=null)
        {
            var expanded = _expanded;

            if (overrideExpanding != null)
            {
                expanded = (bool) overrideExpanding;
                _expandingOverriden = expanded;
            }
            else
            {
                if (_expandingOverriden != null)
                    expanded = (bool) _expandingOverriden;
            }
            
            _expanderLabel.text = !expanded ? "►" : "▼";
            var displayStyle = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            _groupContent.style.display = displayStyle;
        }

        private void HandlePackageSelection(PackageView packageView)
        {
            if (_expandedPackageView == packageView)
            {
                _expandedPackageView = null;
                return;
            }

            if (_expandedPackageView == null)
            {
                _expandedPackageView = packageView;
                return;
            }
            
            // Always where it was
            if (packageView.worldBound.y > _expandedPackageView.worldBound.y)
            {
                var sliderChangeDelta = -(_expandedPackageView.worldBound.height - packageView.worldBound.height);
                OnSliderChange?.Invoke(sliderChangeDelta);
            }
            
            _expandedPackageView?.ShowFunctions(false);
            _expandedPackageView = packageView;
            
        }

        private void UpdateGroupLabel()
        {
            _groupLabel.text = $"{GroupName} ({_packages.Count})";
        }

        private void OverwriteGroupLabel(string text)
        {
            _groupLabel.text = text;
        }
    }
}