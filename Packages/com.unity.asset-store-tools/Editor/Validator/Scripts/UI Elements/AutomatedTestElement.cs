using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssetStoreTools.Validator.UIElements
{
    internal class AutomatedTestElement : VisualElement
    {
        private const string IconsPath = "Packages/com.unity.asset-store-tools/Editor/Validator/Icons";
        
        private readonly AutomatedTest _test;
        private TestResult.ResultStatus _lastStatus;

        private VisualElement _expandedBox;
        private VisualElement _resultMessagesBox;

        private Label _expanderLabel;
        private Button _foldoutBox;
        private Image _testImage;
        
        private bool _expanded;

        public AutomatedTestElement(AutomatedTest test)
        {
            _test = test;
            ConstructAutomatedTest();

            var sceneChangeHandler = new EditorSceneManager.SceneOpenedCallback((_, __) => ResultChanged());
            EditorSceneManager.sceneOpened += sceneChangeHandler;
            AssetStoreValidator.OnWindowDestroyed += () => EditorSceneManager.sceneOpened -= sceneChangeHandler; 
        }

        public AutomatedTest GetAutomatedTest()
        {
            return _test;
        }

        public TestResult.ResultStatus GetLastStatus()
        {
            return _lastStatus;
        }

        public void ResultChanged()
        {
            ClearMessages();

            _testImage.image = GetIconByStatus();
            _lastStatus = _test.Result.Result;

            if (_test.Result.MessageCount == 0)
                return;

            ShowMessages();
        }
        
        private void ClearMessages()
        {
            _resultMessagesBox?.Clear();
        }

        private void ShowMessages()
        {
            var result = _test.Result;

            _resultMessagesBox?.RemoveFromHierarchy();

            _resultMessagesBox = new VisualElement();
            _resultMessagesBox.AddToClassList("result-messages-box");
            
            switch (result.Result)
            {
                case TestResult.ResultStatus.Pass:
                    _resultMessagesBox.AddToClassList("result-messages-box-pass");
                    break;
                case TestResult.ResultStatus.Warning:
                    _resultMessagesBox.AddToClassList("result-messages-box-warning");
                    break;
                case TestResult.ResultStatus.Fail:
                    _resultMessagesBox.AddToClassList("result-messages-box-fail");
                    break;
            }

            _expandedBox.Add(_resultMessagesBox);
            
            for (int i = 0; i < result.MessageCount; i++)
            {
                var resultText = result.GetMessage(i).GetText();
                var clickAction = result.GetMessage(i).ClickAction;
                var messageObjects = result.GetMessage(i).GetMessageObjects();

                var resultMessage = new VisualElement {name = "ResultMessageElement"};
                resultMessage.AddToClassList("information-box");
                
                var informationButton = new Button();
                informationButton.AddToClassList("result-information-button");

                if (result.GetMessage(i).ClickAction != null)
                {
                    informationButton.tooltip = clickAction.ActionTooltip;
                    informationButton.clicked += clickAction.Execute;
                    informationButton.SetEnabled(true);
                }
                
                var informationDescription = new Label {name = "InfoDesc", text = resultText};
                informationDescription.AddToClassList("test-reason-desc");

                informationButton.Add(informationDescription);
                resultMessage.Add(informationButton);

                foreach (var obj in messageObjects)
                {
                    if (obj == null)
                        continue;
                    
                    var objectField = new ObjectField() {objectType = obj.GetType(), value = obj};
                    objectField.RegisterCallback<ChangeEvent<UnityEngine.Object>>((evt) =>
                        objectField.SetValueWithoutNotify(evt.previousValue));
                    resultMessage.Add(objectField);
                }
                
                _resultMessagesBox.Add(resultMessage);

                if (i == result.MessageCount - 1) 
                    continue;
                
                var separator = new VisualElement() {name = "Separator"};
                separator.AddToClassList("message-separator");
                _resultMessagesBox.Add(separator);
            }
        }
        
        private void ShowFunctions(bool? show=null)
        {
            if (show == null)
                _expanded = !_expanded;
            else 
                _expanded = (bool) show;
            
            if (_expanded)
                _foldoutBox.AddToClassList("foldout-box-expanded");
            else
                _foldoutBox.RemoveFromClassList("foldout-box-expanded");

            _expanderLabel.text = !_expanded ? "►" : "▼";
            _expandedBox.style.display = _expanded ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ConstructAutomatedTest()
        {
            name = "TestRow";
            AddToClassList("full-test-box");
            
            _foldoutBox = new Button (() => {ShowFunctions();}) {name = _test.Title};
            _foldoutBox.AddToClassList("foldout-box");
            
            // Expander and Asset Label
            VisualElement labelExpanderRow = new VisualElement { name = "labelExpanderRow" };
            labelExpanderRow.AddToClassList("expander-label-row");

            _expanderLabel = new Label { name = "ExpanderLabel", text = "►" };
            _expanderLabel.AddToClassList("expander");
            
            Label testLabel = new Label { name = "TestLabel", text = _test.Title };
            testLabel.AddToClassList("test-label");
            
            labelExpanderRow.Add(_expanderLabel);
            labelExpanderRow.Add(testLabel);

            _testImage = new Image
            {
                name = "TestImage",
                image = GetIconByStatus()
            };
            
            _testImage.AddToClassList("test-result-image");

            _foldoutBox.Add(labelExpanderRow);
            _foldoutBox.Add(_testImage);

            var testCaseDescription = new TextField
            {
                name = "Description",
                value = _test.Description,
                isReadOnly = true,
                multiline = true,
                focusable = false,
                doubleClickSelectsWord = false,
                tripleClickSelectsLine = false
            };
            testCaseDescription.AddToClassList("test-description");

            _expandedBox = new VisualElement();
            _expandedBox.AddToClassList("test-expanded-box");
            
            _expandedBox.Add(testCaseDescription);
            
            Add(_foldoutBox);
            Add(_expandedBox);
            
            ShowFunctions(false);
            ResultChanged();
        }

        private Texture GetIconByStatus()
        {
            var iconTheme = "";
            if (!EditorGUIUtility.isProSkin)
                iconTheme = "_d";
            
            switch (_test.Result.Result)
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