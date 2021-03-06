using AssetStoreTools.Exporter;
using AssetStoreTools.Utility;
using AssetStoreTools.Utility.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssetStoreTools.Uploader.UIElements
{
    internal class UnityPackageUploadWorkflowView : UploadWorkflowView
    {
        public const string WorkflowName = "UnitypackageWorkflow";
        public const string WorkflowDisplayName = "Pre-exported .unitypackage";

        public override string Name => WorkflowName;
        public override string DisplayName => WorkflowDisplayName;

        private UnityPackageUploadWorkflowView(string category, Action serializeSelection) : base(serializeSelection)
        {
            Category = category;
            SetupWorkflow();
        }

        public static UnityPackageUploadWorkflowView Create(string category, Action serializeAction)
        {
            return new UnityPackageUploadWorkflowView(category, serializeAction);
        }

        protected sealed override void SetupWorkflow()
        {
            // Path selection
            VisualElement folderPathSelectionRow = new VisualElement();
            folderPathSelectionRow.AddToClassList("selection-box-row");

            VisualElement labelHelpRow = new VisualElement();
            labelHelpRow.AddToClassList("label-help-row");

            Label folderPathLabel = new Label { text = "Package path" };
            Image folderPathLabelTooltip = new Image
            {
                tooltip = "Select the .unitypackage file you would like to upload."
            };

            labelHelpRow.Add(folderPathLabel);
            labelHelpRow.Add(folderPathLabelTooltip);

            PathSelectionField = new TextField();
            PathSelectionField.AddToClassList("path-selection-field");
            PathSelectionField.isReadOnly = true;

            Button browsePathButton = new Button(BrowsePath) { name = "BrowsePathButton", text = "Browse" };
            browsePathButton.AddToClassList("browse-button");

            folderPathSelectionRow.Add(labelHelpRow);
            folderPathSelectionRow.Add(PathSelectionField);
            folderPathSelectionRow.Add(browsePathButton);

            Add(folderPathSelectionRow);

            ValidationElement = new PackageValidationElement();
            Add(ValidationElement);

            ValidationElement.SetCategory(Category);
        }

        public override void LoadSerializedWorkflow(JsonValue json, string lastUploadedPath, string lastUploadedGuid)
        {
            if (!DeserializeMainExportPath(json, out string mainPackagePath) || !File.Exists(mainPackagePath))
            {
                ASDebug.Log("Unable to restore .unitypackage upload workflow path from the local cache");
                LoadSerializedWorkflowFallback(lastUploadedPath, lastUploadedGuid);
                return;
            }

            ASDebug.Log($"Restoring serialized .unitypackage workflow values from local cache");
            HandleUnityPackageUploadPathSelection(mainPackagePath, false);
        }

        public override void LoadSerializedWorkflowFallback(string lastUploadedPath, string lastUploadedGuid)
        {
            var packagePath = AssetDatabase.GUIDToAssetPath(lastUploadedGuid);
            if (string.IsNullOrEmpty(packagePath))
                packagePath = lastUploadedPath;

            if (!packagePath.EndsWith(".unitypackage") || !File.Exists(packagePath))
            {
                ASDebug.Log("Unable to restore .unitypackage workflow path from previous upload values");
                return;
            }

            ASDebug.Log($"Restoring serialized .unitypackage workflow values from previous upload values");
            HandleUnityPackageUploadPathSelection(packagePath, false);
        }

        protected override void BrowsePath()
        {
            // Path retrieval
            var rootProjectPath = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
            var absolutePackagePath = EditorUtility.OpenFilePanel("Select a .unitypackage file", rootProjectPath, "unitypackage");

            if (string.IsNullOrEmpty(absolutePackagePath))
                return;

            var relativeExportPath = string.Empty;
            if (absolutePackagePath.StartsWith(rootProjectPath))
                relativeExportPath = absolutePackagePath.Substring(rootProjectPath.Length);
            var selectedPackagePath = !string.IsNullOrEmpty(relativeExportPath) ? relativeExportPath : absolutePackagePath;

            HandleUnityPackageUploadPathSelection(selectedPackagePath, true);
        }

        private void HandleUnityPackageUploadPathSelection(string selectedPackagePath, bool serializeValues)
        {
            // Main upload path
            PathSelectionField.value = selectedPackagePath;

            // Export data
            MainExportPath = selectedPackagePath;

            LocalPackageGuid = string.Empty;
            LocalPackagePath = string.Empty;
            LocalProjectPath = selectedPackagePath;

            if (serializeValues)
                SerializeSelection?.Invoke();

            ValidationElement.SetValidationPaths(MainExportPath);
        }

        public override Task<ExportResult> ExportPackage(string __, bool _)
        {
            if (string.IsNullOrEmpty(MainExportPath))
                return Task.FromResult(new ExportResult() { Success = false, Error = ASError.GetGenericError(new ArgumentException("Package export path is empty or null")) });
            return Task.FromResult(new ExportResult() { Success = true, ExportedPath = MainExportPath });
        }
    }
}