using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssetStoreTools.Uploader.UIElements
{
    internal abstract class ValidationElement : VisualElement
    {
        protected Button ValidateButton;
        
        protected VisualElement InfoBox;
        protected Label InfoBoxLabel;
        protected Image InfoBoxImage;

        protected string Category;

        public ValidationElement()
        {
            ConstructValidationElement();
            SetupInfoBox(string.Empty);
            EnableValidation(false);
        }
        
        public void SetCategory(string category)
        {
            Category = category;
        }

        private void ConstructValidationElement()
        {
            VisualElement validatorButtonRow = new VisualElement();
            validatorButtonRow.AddToClassList("selection-box-row");

            VisualElement validatorLabelHelpRow = new VisualElement();
            validatorLabelHelpRow.AddToClassList("label-help-row");

            Label validatorLabel = new Label { text = "Validation" };
            Image validatorLabelTooltip = new Image
            {
                tooltip = "You can use the Asset Store Validator to check your package for common publishing issues"
            };
            
            ValidateButton = new Button(RunValidation) { name = "ValidateButton", text = "Validate" };
            ValidateButton.AddToClassList("validation-button");
            
            validatorLabelHelpRow.Add(validatorLabel);
            validatorLabelHelpRow.Add(validatorLabelTooltip);

            validatorButtonRow.Add(validatorLabelHelpRow);
            validatorButtonRow.Add(ValidateButton);

            Add(validatorButtonRow);
        }

        protected void EnableValidation(bool enable)
        {
            style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;
        }

        protected void PopulateInfoBox(bool hasCompilationErrors, int errorCount, int warningCount)
        {
            Texture infoImage = null;
            var infoText = string.Empty;

            if (hasCompilationErrors)
            {
                infoImage = EditorGUIUtility.IconContent("console.erroricon@2x").image;
                infoText += "• Package caused compilation errors\n";
            }
            if (errorCount > 0)
            {
                infoImage = EditorGUIUtility.IconContent("console.erroricon@2x").image;
                infoText += $"• Validation reported {errorCount} error(s)\n";
            }
            if (warningCount > 0)
            {
                if (infoImage == null)
                    infoImage = EditorGUIUtility.IconContent("console.warnicon@2x").image;
                infoText += $"• Validation reported {warningCount} warning(s)\n";
            }

            if (string.IsNullOrEmpty(infoText))
            {
                infoText = "No issues were found!";
                infoImage = InfoBoxImage.image = EditorGUIUtility.IconContent("console.infoicon@2x").image;
            }
            else
                infoText = infoText.Substring(0, infoText.Length - "\n".Length);

            InfoBoxImage.image = infoImage;
            InfoBoxLabel.text = infoText;
        }

        protected abstract void SetupInfoBox(string infoText);
        public abstract void SetValidationPaths(params string[] paths);
        protected abstract void RunValidation();
        public abstract bool GetValidationSummary(out string validationSummary);
    }
}