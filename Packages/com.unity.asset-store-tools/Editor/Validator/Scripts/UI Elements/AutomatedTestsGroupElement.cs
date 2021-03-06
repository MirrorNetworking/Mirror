using AssetStoreTools.Validator.Data;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssetStoreTools.Validator.UIElements
{
    internal class AutomatedTestsGroupElement : VisualElement
    {
        private const string IconsPath = "Packages/com.unity.asset-store-tools/Editor/Validator/Icons";

        private string GroupName { get; }
        private TestResult.ResultStatus GroupStatus { get; }
        private readonly List<AutomatedTestElement> _tests;
        
        private Button _groupExpanderBox;
        private Image _groupImage;
        private VisualElement _groupContent;
        
        private Label _expanderLabel;
        private Label _groupLabel;
        
        private bool _expanded;
        
        public AutomatedTestsGroupElement(string groupName, TestResult.ResultStatus groupStatus, bool createExpanded)
        {
            GroupName = groupName;
            GroupStatus = groupStatus;
            AddToClassList("tests-group");
            
            _tests = new List<AutomatedTestElement>();
            _expanded = createExpanded;
            
            SetupSingleGroupElement();
            HandleExpanding();
        }

        public void AddTest(AutomatedTestElement test)
        {
            _tests.Add(test);
            _groupContent.Add(test);

            UpdateGroupLabel();
            ShowGroup();
        }

        public void RemoveTest(AutomatedTestElement test)
        {
            if (_tests.Contains(test))
                _tests.Remove(test);
            
            if (_groupContent.Contains(test))
                _groupContent.Remove(test);

            ShowGroup();
        }

        public void AddSeparator()
        {
            var groupSeparator = new VisualElement {name = "GroupSeparator"};
            groupSeparator.AddToClassList("group-separator");
            
            _groupContent.Add(groupSeparator);
        }

        private void ShowGroup()
        {
            style.display = _tests.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }
        
        private void SetupSingleGroupElement()
        {
            _groupExpanderBox = new Button();
            _groupExpanderBox.AddToClassList("group-expander-box");
            
            _expanderLabel = new Label { name = "ExpanderLabel", text = "►" };
            _expanderLabel.AddToClassList("expander");

            _groupImage = new Image
            {
                name = "TestImage",
                image = GetIconByStatus()
            };
            _groupImage.AddToClassList("group-image");

            _groupLabel = new Label {text = $"{GroupName} ({_tests.Count})"};
            _groupLabel.AddToClassList("group-label");
            
            _groupExpanderBox.Add(_expanderLabel);
            _groupExpanderBox.Add(_groupImage);
            _groupExpanderBox.Add(_groupLabel);

            _groupContent = new VisualElement {name = "GroupContentBox"};
            _groupContent.AddToClassList("group-content-box");

            _groupExpanderBox.clicked += () =>
            {
                _expanded = !_expanded;
                
                HandleExpanding();
            };

            Add(_groupExpanderBox);
            Add(_groupContent);
        }

        private void HandleExpanding()
        {
            var expanded = _expanded;

            _expanderLabel.text = !expanded ? "►" : "▼";
            var displayStyle = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            _groupContent.style.display = displayStyle;
        }

        private void UpdateGroupLabel()
        {
            _groupLabel.text = $"{GroupName} ({_tests.Count})";
        }
        
        private Texture GetIconByStatus()
        {
            var iconTheme = "";
            if (!EditorGUIUtility.isProSkin)
                iconTheme = "_d";
            
            switch (GroupStatus)
            {
                case TestResult.ResultStatus.Pass:
                    return (Texture) EditorGUIUtility.Load($"{IconsPath}/success{iconTheme}.png");
                case TestResult.ResultStatus.Warning:
                    return (Texture) EditorGUIUtility.Load($"{IconsPath}/warning{iconTheme}.png");
                case TestResult.ResultStatus.Fail:
                    return (Texture) EditorGUIUtility.Load($"{IconsPath}/error{iconTheme}.png");
                case TestResult.ResultStatus.Undefined:
                    return (Texture) EditorGUIUtility.Load($"{IconsPath}/undefined{iconTheme}.png"); 
                default:
                    return null;
            }
        }
    }
}