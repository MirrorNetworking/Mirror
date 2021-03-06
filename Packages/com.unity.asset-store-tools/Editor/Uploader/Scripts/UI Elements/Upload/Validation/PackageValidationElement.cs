using AssetStoreTools.Utility;
using AssetStoreTools.Validator;
using AssetStoreTools.Validator.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssetStoreTools.Uploader.UIElements
{
    internal class PackageValidationElement : ValidationElement
    {
        private string _packagePath;
        private ValidationStateData _packageValidationStateData;

        private VisualElement _projectButtonContainer;

        protected override void SetupInfoBox(string infoText)
        {
            InfoBox = new Box { name = "InfoBox" };
            InfoBox.style.display = DisplayStyle.None;
            InfoBox.AddToClassList("info-box");

            InfoBoxImage = new Image();
            InfoBoxLabel = new Label { name = "ValidationLabel", text = infoText };

            _projectButtonContainer = new VisualElement() { name = "Button Container" };
            _projectButtonContainer.AddToClassList("hyperlink-button-container");

            InfoBox.Add(InfoBoxImage);
            InfoBox.Add(InfoBoxLabel);
            InfoBox.Add(_projectButtonContainer);

            Add(InfoBox);
        }

        public override void SetValidationPaths(params string[] paths)
        {
            if (paths == null || paths.Length != 1)
                throw new ArgumentException("Package Validation only accepts a single path argument");

            _packagePath = paths[0];
            EnableValidation(true);
        }

        protected override void RunValidation()
        {
            if (!EditorUtility.DisplayDialog("Notice", "Pre-exported package validation is performed in a separate temporary project. " +
                "It may take some time for the temporary project to be created, which will halt any actions in the current project. " +
                "The current project will resume work after the temporary project is exited.\n\nDo you wish to proceed?", "Yes", "No"))
                return;

            var validationSettings = new ValidationSettings() 
            {
                ValidationPaths = new List<string> { _packagePath },
                Category = Category 
            };
            var validationResult = PackageValidator.ValidatePreExportedPackage(validationSettings, false);

            switch (validationResult.Status)
            {
                case ValidationStatus.RanToCompletion:
                    DisplayValidationResult(validationResult.ProjectPath);
                    break;
                case ValidationStatus.Failed:
                    EditorUtility.DisplayDialog("Error", validationResult.Error, "OK");
                    break;
                case ValidationStatus.Cancelled:
                    ASDebug.Log("Validation cancelled");
                    break;
                case ValidationStatus.NotRun:
                    throw new InvalidOperationException("Validation was not run. Please report this issue");
                default:
                    throw new ArgumentException("Received an invalid validation status");
            }
        }

        public override bool GetValidationSummary(out string validationSummary)
        {
            validationSummary = string.Empty;

            if (_packageValidationStateData == null)
                return false;

            return ValidationState.GetValidationSummaryJson(_packageValidationStateData, out validationSummary);
        }

        private void DisplayValidationResult(string projectPath)
        {
            // Retrieve the validation state data from the validation project
            var validationStateDataPath = $"{projectPath}/{ValidationState.PersistentDataLocation}/{ValidationState.ValidationDataFilename}";
            if (File.Exists(validationStateDataPath))
                _packageValidationStateData = JsonUtility.FromJson<ValidationStateData>(File.ReadAllText(validationStateDataPath));

            var validationComplete = _packageValidationStateData != null;
            EnableInfoBox(true, validationComplete, projectPath);
        }

        private void EnableInfoBox(bool enable, bool validationComplete, string projectPath)
        {
            if (!enable)
            {
                InfoBox.style.display = DisplayStyle.None;
                return;
            }

            if (!validationComplete)
            {
                InfoBoxImage.image = EditorGUIUtility.IconContent("console.erroricon@2x").image;
                InfoBoxLabel.text = "Validation status unknown. Please report this issue";
                return;
            }

            InfoBox.style.display = DisplayStyle.Flex;

            var compilationFailed = _packageValidationStateData.HasCompilationErrors;
            var failCount = _packageValidationStateData.SerializedValues.Count(x => x.Result.Result == TestResult.ResultStatus.Fail);
            var warningCount = _packageValidationStateData.SerializedValues.Count(x => x.Result.Result == TestResult.ResultStatus.Warning);

            PopulateInfoBox(compilationFailed, failCount, warningCount);

            _projectButtonContainer.Clear();
            var openButton = new Button(() => OpenTemporaryProject(projectPath)) { text = "Open Project" };
            openButton.AddToClassList("hyperlink-button");
            var saveButton = new Button(() => SaveTemporaryProject(projectPath)) { text = "Save Project" };
            saveButton.AddToClassList("hyperlink-button");

            _projectButtonContainer.Add(openButton);
            _projectButtonContainer.Add(saveButton);
        }

        private void OpenTemporaryProject(string projectPath)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Waiting...", "Validation project is open. Waiting for it to exit...", 0.4f);

#if UNITY_EDITOR_OSX
                var unityPath = Path.Combine(EditorApplication.applicationPath, "Contents", "MacOS", "Unity");
#else
                var unityPath = EditorApplication.applicationPath;
#endif

                var logFilePath = $"{projectPath}/editor.log";

                var processInfo = new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = unityPath,
                    Arguments = $"-projectPath \"{projectPath}\" -logFile \"{logFilePath}\" -executeMethod AssetStoreTools.AssetStoreTools.ShowAssetStoreToolsValidator"
                };

                using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                    process.WaitForExit();
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void SaveTemporaryProject(string projectPath)
        {
            try
            {
                var savePath = EditorUtility.SaveFolderPanel("Select a folder", Environment.GetFolderPath(Environment.SpecialFolder.Desktop), string.Empty);
                if (string.IsNullOrEmpty(savePath))
                    return;

                var saveDir = new DirectoryInfo(savePath);
                if(!saveDir.Exists || saveDir.GetFileSystemInfos().Length != 0)
                {
                    EditorUtility.DisplayDialog("Saving project failed", "Selected directory must be an empty folder", "OK");
                    return;
                }

                EditorUtility.DisplayProgressBar("Saving...", "Saving project...", 0.4f);
                FileUtility.CopyDirectory(projectPath, savePath, true);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}