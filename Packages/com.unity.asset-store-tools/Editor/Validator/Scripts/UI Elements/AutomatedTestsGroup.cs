using AssetStoreTools.Validator.Categories;
using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.Utility;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace AssetStoreTools.Validator.UIElements
{
    internal class AutomatedTestsGroup : VisualElement
    {       
        private readonly Dictionary<int, AutomatedTestElement> _testElements = new Dictionary<int, AutomatedTestElement>();
        private readonly Dictionary<TestResult.ResultStatus, AutomatedTestsGroupElement> _testGroupElements = 
            new Dictionary<TestResult.ResultStatus, AutomatedTestsGroupElement>();

        private ScrollView _allTestsScrollView;
        private ValidationInfoElement _validationInfoBox;
        private PathBoxElement _pathBox;
        private Button _validateButton;
        private ToolbarMenu _categoryMenu;

        private static readonly TestResult.ResultStatus[] StatusOrder = {TestResult.ResultStatus.Undefined, 
            TestResult.ResultStatus.Fail, TestResult.ResultStatus.Warning, TestResult.ResultStatus.Pass, 
            TestResult.ResultStatus.VariableSeverityIssue}; // VariableSeverityFail should always be convered to Warning/Fail, but is defined as a failsafe

        private PackageValidator _validator;
        private string _selectedCategory;

        public AutomatedTestsGroup()
        {
            _validator = PackageValidator.Instance;
            ConstructInfoPart();
            ConstructAutomatedTests();

            ValidationState.Instance.OnJsonSave -= Reinitialize;
            ValidationState.Instance.OnJsonSave += Reinitialize;
        }

        private void Reinitialize()
        {
            this.Clear();
            
            _testElements.Clear();
            _testGroupElements.Clear();
            
            ConstructInfoPart();
            ConstructAutomatedTests();
        }

        private void ConstructInfoPart()
        {
            _validationInfoBox = new ValidationInfoElement();
            _pathBox = new PathBoxElement();
            
            var categorySelectionBox = new VisualElement();
            categorySelectionBox.AddToClassList("selection-box-row");
            
            VisualElement labelHelpRow = new VisualElement();
            labelHelpRow.AddToClassList("label-help-row");
            
            Label categoryLabel = new Label { text = "Category" };
            Image categoryLabelTooltip = new Image
            {
                tooltip = "Choose a base category of your package" +
                          "\n\nThis can be found in the Publishing Portal when creating the package listing or just " +
                          "selecting a planned one." +
                          "\n\nNote: Different categories could have different severities of several test cases."
            };
            
            labelHelpRow.Add(categoryLabel);
            labelHelpRow.Add(categoryLabelTooltip);
            
            _categoryMenu = new ToolbarMenu {name = "CategoryMenu"};
            _categoryMenu.AddToClassList("category-menu");
            PopulateCategoryDropdown();
            
            categorySelectionBox.Add(labelHelpRow);
            categorySelectionBox.Add(_categoryMenu);

            _validateButton = new Button(RunAllTests) {text = "Validate"};
            _validateButton.AddToClassList("run-all-button");

            _validationInfoBox.Add(categorySelectionBox);
            _validationInfoBox.Add(_pathBox);
            _validationInfoBox.Add(_validateButton);
            
            Add(_validationInfoBox);
        }

        private void ConstructAutomatedTests()
        {
            name = "AutomatedTests";

            _allTestsScrollView = new ScrollView
            {
                viewDataKey = "scrollViewKey",
            };
            _allTestsScrollView.AddToClassList("tests-scroll-view");

            var groupedTests = GroupTestsByStatus(_validator.AutomatedTests);

            foreach (var status in StatusOrder)
            {
                var group = new AutomatedTestsGroupElement(status.ToString(), status, true);
                _testGroupElements.Add(status, group);
                _allTestsScrollView.Add(group);
                
                if (!groupedTests.ContainsKey(status))
                    continue;
                
                foreach (var test in groupedTests[status])
                {
                    var testElement = new AutomatedTestElement(test);
                    
                    _testElements.Add(test.Id, testElement);
                    group.AddTest(testElement);
                }
                
                if (StatusOrder[StatusOrder.Length - 1] != status)
                    group.AddSeparator();
            }

            Add(_allTestsScrollView);
        }

        private void PopulateCategoryDropdown()
        {
            var list = _categoryMenu.menu;

            var validationStateData = ValidationState.Instance.ValidationStateData;
            
            HashSet<string> categories = new HashSet<string>();
            var testData = ValidatorUtility.GetAutomatedTestCases();
            foreach (var test in testData)
            {
                AddCategoriesToSet(categories, test.CategoryInfo);
            }

            foreach (var category in categories)
            {
                list.AppendAction(ConvertSlashToUnicodeSlash(category), _ => OnCategoryValueChange(category));
            }
            list.AppendAction("Other", _ => OnCategoryValueChange(string.Empty));

            _selectedCategory = _validator.Category;
            if (validationStateData.SerializedKeys == null)
                _categoryMenu.text = "Select Category";
            else if (string.IsNullOrEmpty(_selectedCategory))
                _categoryMenu.text = "Other";
            else
                _categoryMenu.text = _validator.Category;
        }
        
        private string ConvertSlashToUnicodeSlash(string text)
        {
            return text.Replace('/', '\u2215');
        }

        private void AddCategoriesToSet(HashSet<string> set, ValidatorCategory category)
        {
            if (category == null)
                return;
            
            foreach (var filter in category.Filter)
                set.Add(filter);
        }

        private Dictionary<TestResult.ResultStatus, List<AutomatedTest>> GroupTestsByStatus(List<AutomatedTest> tests)
        {
            var groupedDictionary = new Dictionary<TestResult.ResultStatus, List<AutomatedTest>>();
            
            foreach (var t in tests)
            {
                if (!groupedDictionary.ContainsKey(t.Result.Result))
                    groupedDictionary.Add(t.Result.Result, new List<AutomatedTest>());
                
                groupedDictionary[t.Result.Result].Add(t);
            }

            return groupedDictionary;
        }

        private async void RunAllTests()
        {
            var validationPaths = _pathBox.GetValidationPaths();
            if (validationPaths.Count == 0)
                return;

            _validateButton.SetEnabled(false);

            // Make sure everything is collected and validation button is disabled
            await Task.Delay(100);

            var validationSettings = new ValidationSettings()
            {
                ValidationPaths = validationPaths,
                Category = _selectedCategory
            };

            var validationResult = _validator.RunAllTests(validationSettings);

            if(validationResult.Status != ValidationStatus.RanToCompletion)
            {
                EditorUtility.DisplayDialog("Validation failed", $"Package validation failed: {validationResult.Error}", "OK");
                return;
            }

            // Update UI
            foreach(var test in validationResult.AutomatedTests)
            {
                var testElement = _testElements[test.Id];

                var currentStatus = test.Result.Result;
                var lastStatus = testElement.GetLastStatus();

                if (_testGroupElements.ContainsKey(lastStatus) && _testGroupElements.ContainsKey(currentStatus))
                {
                    if (lastStatus != currentStatus)
                    {
                        _testGroupElements[lastStatus].RemoveTest(testElement);
                        _testGroupElements[currentStatus].AddTest(testElement);

                        testElement.GetAutomatedTest().Result = test.Result;
                    }
                }

                testElement.ResultChanged();
            }

            _validateButton.SetEnabled(true);
        }
        
        private void OnCategoryValueChange(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                _categoryMenu.text = "Other";
                _selectedCategory = value;
            }
            else
            {
                _categoryMenu.text = value;
                _selectedCategory = value;
            } 
        }
    }
}