using AssetStoreTools.Validator;
using AssetStoreTools.Validator.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine.UIElements;

namespace AssetStoreTools.Uploader.UIElements
{
    internal class AssetValidationElement : ValidationElement
    {
        private Button _viewReportButton;

        private string[] _validationPaths;

        protected override void SetupInfoBox(string infoText)
        {
            InfoBox = new Box { name = "InfoBox" };
            InfoBox.style.display = DisplayStyle.None;
            InfoBox.AddToClassList("info-box");

            InfoBoxImage = new Image();
            InfoBoxLabel = new Label { name = "ValidationLabel", text = infoText };
            _viewReportButton = new Button(ViewReport) { text = "View report" };
            _viewReportButton.AddToClassList("hyperlink-button");

            InfoBox.Add(InfoBoxImage);
            InfoBox.Add(InfoBoxLabel);
            InfoBox.Add(_viewReportButton);

            Add(InfoBox);
        }

        public override void SetValidationPaths(params string[] paths)
        {
            _validationPaths = paths;
            EnableValidation(true);
        }

        protected override async void RunValidation()
        {
            ValidateButton.SetEnabled(false);

            // Make sure everything is collected and validation button is disabled
            await Task.Delay(100);

            var outcomeList = new List<TestResult>();

            var validationSettings = new ValidationSettings()
            {
                ValidationPaths = _validationPaths.ToList(),
                Category = Category
            };

            var validationResult = PackageValidator.ValidatePackage(validationSettings);

            if(validationResult.Status != ValidationStatus.RanToCompletion)
            {
                EditorUtility.DisplayDialog("Validation failed", $"Package validation failed: {validationResult.Error}", "OK");
                return;
            }

            foreach (var test in validationResult.AutomatedTests)
                outcomeList.Add(test.Result);

            EnableInfoBox(true, validationResult.HadCompilationErrors, outcomeList);
            ValidateButton.SetEnabled(true);
        }

        private void ViewReport()
        {
            var validationStateData = ValidationState.Instance.ValidationStateData;

            // Re-run validation if paths are out of sync
            if (validationStateData.SerializedValidationPaths.Count != _validationPaths.Length ||
                !validationStateData.SerializedValidationPaths.All(_validationPaths.Contains))
                RunValidation();

            // Re-run validation if category is out of sync
            if (validationStateData.SerializedCategory != Category)
                RunValidation();

            // Show the Validator
            AssetStoreTools.ShowAssetStoreToolsValidator();
        }

        private void EnableInfoBox(bool enable, bool hasCompilationErrors, List<TestResult> outcomeList)
        {
            if (!enable)
            {
                InfoBox.style.display = DisplayStyle.None;
                return;
            }

            var errorCount = outcomeList.Count(x => x.Result == TestResult.ResultStatus.Fail);
            var warningCount = outcomeList.Count(x => x.Result == TestResult.ResultStatus.Warning);

            PopulateInfoBox(hasCompilationErrors, errorCount, warningCount);

            ValidateButton.text = "Re-validate";
            InfoBox.style.display = DisplayStyle.Flex;
        }

        public override bool GetValidationSummary(out string validationSummary)
        {
            validationSummary = string.Empty;

            if (string.IsNullOrEmpty(InfoBoxLabel.text))
                return false;

            var data = ValidationState.Instance.ValidationStateData;
            return ValidationState.GetValidationSummaryJson(data, out validationSummary);
        }
    }
}