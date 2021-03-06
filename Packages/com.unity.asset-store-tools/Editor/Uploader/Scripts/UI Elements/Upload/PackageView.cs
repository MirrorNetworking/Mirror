using AssetStoreTools.Utility.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using AssetStoreTools.Utility;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using AssetStoreTools.Exporter;
using AssetStoreTools.Uploader.Data;
using AssetStoreTools.Uploader.Utility;

namespace AssetStoreTools.Uploader.UIElements
{
    internal class PackageView : VisualElement
    {
        public string PackageId => _packageData.Id;
        public string VersionId => _packageData.VersionId;
        public string PackageName => _packageData.Name;
        public string Status => _packageData.Status;
        public string Category => _packageData.Category;
        public string LastUpdatedDate => FormatDate(_packageData.LastDate);
        public string LastUpdatedSize => FormatSize(_packageData.LastSize);
        public bool IsCompleteProject => _packageData.IsCompleteProject;
        public string LastUploadedPath => _packageData.LastUploadedPath;
        public string LastUploadedGuid => _packageData.LastUploadedGuid;
        public string SearchableText { get; private set; }
        
        private PackageData _packageData;

        // Unexpanded state dynamic elements
        private Button _foldoutBox;
        private Label _expanderLabel;
        private Label _assetLabel;
        private Label _lastDateSizeLabel;
        private Button _openInBrowserButton;
        
        // Expanded state dynamic elements
        private VisualElement _functionsBox;

        private VisualElement _exportAndUploadContainer;
        private VisualElement _uploadProgressContainer;
        private Button _exportButton;
        private Button _uploadButton;
        private Button _cancelUploadButton;
        private ProgressBar _uploadProgressBarFlow;
        private ProgressBar _uploadProgressBarHeader;

        private VisualElement _uploadProgressFlowBg;
        private VisualElement _uploadProgressHeaderBg;

        private bool _expanded;
        public Action<PackageView> OnPackageSelection;
        
        private VisualElement _workflowSelectionBox;
        private UploadWorkflowView _activeWorkflowElement;
        private Dictionary<string, UploadWorkflowView> _uploadWorkflows;

        public PackageView(PackageData packageData)
        {
            UpdateDataValues(packageData);
            SetupPackageElement();
        }

        public void UpdateDataValues(PackageData packageData)
        {
            _packageData = packageData;

            SearchableText = $"{PackageName} {Category}".ToLower();

            if (_foldoutBox == null)
                return;

            _assetLabel.text = PackageName;
            _lastDateSizeLabel.text = $"{Category} | {LastUpdatedSize} | {LastUpdatedDate}";

            if (_uploadWorkflows != null && _uploadWorkflows.ContainsKey(FolderUploadWorkflowView.WorkflowName))
                ((FolderUploadWorkflowView) _uploadWorkflows[FolderUploadWorkflowView.WorkflowName]).SetCompleteProject(packageData.IsCompleteProject);

            if (Status == "draft")
                return;
            
            ResetPostUpload();
            SetupExpander();
        }
        
        public void ShowFunctions(bool show)
        {
            if (_functionsBox == null)
            {
                if (show)
                    SetupFunctionsElement();
                else
                    return;
            }

            if (show == _expanded)
                return;
            
            _expanded = show;
            _expanderLabel.text = !_expanded ? "►" : "▼";
            
            if (_expanded)
                _foldoutBox.AddToClassList("foldout-box-expanded");
            else
                _foldoutBox.RemoveFromClassList("foldout-box-expanded");

            if (_functionsBox != null) 
                _functionsBox.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SetupPackageElement()
        { 
            AddToClassList("full-package-box");

            _foldoutBox = new Button {name = "Package"};
            _foldoutBox.AddToClassList("foldout-box");

            // Expander, Icon and Asset Label
            VisualElement foldoutBoxInfo = new VisualElement { name = "foldoutBoxInfo" };
            foldoutBoxInfo.AddToClassList("foldout-box-info");

            VisualElement labelExpanderRow = new VisualElement { name = "labelExpanderRow" };
            labelExpanderRow.AddToClassList("expander-label-row");

            _expanderLabel = new Label { name = "ExpanderLabel", text = "►" };
            _expanderLabel.AddToClassList("expander");
            
            Image assetImage = new Image { name = "AssetImage" };
            assetImage.AddToClassList("package-image");
            
            VisualElement assetLabelInfoBox = new VisualElement { name = "assetLabelInfoBox" };
            assetLabelInfoBox.AddToClassList("asset-label-info-box");

            _assetLabel = new Label { name = "AssetLabel", text = PackageName };
            _assetLabel.AddToClassList("asset-label");
            
            _lastDateSizeLabel = new Label {name = "AssetInfoLabel", text = $"{Category} | {LastUpdatedSize} | {LastUpdatedDate}"};
            _lastDateSizeLabel.AddToClassList("asset-info");
            
            assetLabelInfoBox.Add(_assetLabel);
            assetLabelInfoBox.Add(_lastDateSizeLabel);

            labelExpanderRow.Add(_expanderLabel);
            labelExpanderRow.Add(assetImage);
            labelExpanderRow.Add(assetLabelInfoBox);

            _openInBrowserButton = new Button
            {
                name = "OpenInBrowserButton",
                tooltip = "View your package in the Publishing Portal."
            };
            _openInBrowserButton.AddToClassList("open-in-browser-button");

            // Header Progress bar
            _uploadProgressBarHeader = new ProgressBar { name = "HeaderProgressBar" };
            _uploadProgressBarHeader.AddToClassList("header-progress-bar");
            _uploadProgressBarHeader.style.display = DisplayStyle.None;
            _uploadProgressHeaderBg = _uploadProgressBarHeader.Q<VisualElement>(className:"unity-progress-bar__progress");

            // Connect it all
            foldoutBoxInfo.Add(labelExpanderRow);
            foldoutBoxInfo.Add(_openInBrowserButton);

            _foldoutBox.Add(foldoutBoxInfo);
            _foldoutBox.Add(_uploadProgressBarHeader);

            Add(_foldoutBox);
            SetupExpander();
        }

        private void SetupExpander()
        {
            if (_foldoutBox != null)
            {
                _foldoutBox.clickable = null;

                // If not draft - hide expander, open a listing page on click
                if (Status != "draft")
                {
                    _expanderLabel.style.display = DisplayStyle.None;
                    _foldoutBox.clicked += () =>
                    {
                        Application.OpenURL($"https://publisher.unity.com/packages/{VersionId}/edit/upload");
                    };
                }
                else
                {
                    // Else open functions box
                    _foldoutBox.clicked += () =>
                    {
                        OnPackageSelection?.Invoke(this);
                        ShowFunctions(!_expanded);
                    };
                }
            }

            if (_openInBrowserButton != null)
            {
                _openInBrowserButton.clickable = null;

                _openInBrowserButton.clicked += () =>
                {
                    Application.OpenURL($"https://publisher.unity.com/packages/{VersionId}/edit/upload");
                };
            }
        }

        private void SetupFunctionsElement()
        {
            _functionsBox = new VisualElement { name = "FunctionalityBox" };
            _functionsBox.AddToClassList("functionality-box");

            _functionsBox.style.display = DisplayStyle.None;

            // Validation and uploading boxes
            var uploadingWorkflow = ConstructUploadingWorkflow();
            _functionsBox.Add(uploadingWorkflow);

            Add(_functionsBox);
        }

        private VisualElement ConstructUploadingWorkflow()
        {
            // Upload Box
            VisualElement uploadBox = new VisualElement { name = "UploadBox" };
            uploadBox.AddToClassList("upload-box");

            var folderUploadWorkflow = FolderUploadWorkflowView.Create(Category, IsCompleteProject, SerializeWorkflowSelections);
            var unitypackageUploadWorkflow = UnityPackageUploadWorkflowView.Create(Category, SerializeWorkflowSelections);
            var hybridPackageUploadWorkflow = HybridPackageUploadWorkflowView.Create(Category, SerializeWorkflowSelections);

            // Workflow selection
            _workflowSelectionBox = new VisualElement();
            _workflowSelectionBox.AddToClassList("selection-box-row");

            VisualElement labelHelpRow = new VisualElement();
            labelHelpRow.AddToClassList("label-help-row");

            Label workflowLabel = new Label { text = "Upload type" };
            Image workflowLabelTooltip = new Image
            {
                tooltip = "Select what content you are uploading to the Asset Store"
                + "\n\n• From Assets Folder - content located within the project's 'Assets' folder or one of its subfolders"
                + "\n\n• Pre-exported .unitypackage - content that has already been compressed into a .unitypackage file"
#if UNITY_ASTOOLS_EXPERIMENTAL
                + "\n\n• Local UPM Package - content that is located within the project's 'Packages' folder. Only embedded and local packages are supported"
#endif
            };

            labelHelpRow.Add(workflowLabel);
            labelHelpRow.Add(workflowLabelTooltip);

            var flowDrop = new ToolbarMenu();
            flowDrop.menu.AppendAction(FolderUploadWorkflowView.WorkflowDisplayName, _ => { SetActiveWorkflowElement(folderUploadWorkflow, flowDrop); });
            flowDrop.menu.AppendAction(UnityPackageUploadWorkflowView.WorkflowDisplayName, _ => { SetActiveWorkflowElement(unitypackageUploadWorkflow, flowDrop); });
#if UNITY_ASTOOLS_EXPERIMENTAL
            flowDrop.menu.AppendAction(HybridPackageUploadWorkflowView.WorkflowDisplayName, _ => { SetActiveWorkflowElement(hybridPackageUploadWorkflow, flowDrop); });
#endif // UNITY_ASTOOLS_EXPERIMENTAL
            flowDrop.AddToClassList("workflow-dropdown");

            _workflowSelectionBox.Add(labelHelpRow);
            _workflowSelectionBox.Add(flowDrop);

            uploadBox.Add(_workflowSelectionBox);

            _uploadWorkflows = new Dictionary<string, UploadWorkflowView>
            {
                {FolderUploadWorkflowView.WorkflowName, folderUploadWorkflow},
                {UnityPackageUploadWorkflowView.WorkflowName, unitypackageUploadWorkflow},
                {HybridPackageUploadWorkflowView.WorkflowName, hybridPackageUploadWorkflow}
            };

            foreach (var kvp in _uploadWorkflows)
                uploadBox.Add(kvp.Value);

            var progressUploadBox = SetupProgressUploadBox();
            uploadBox.Add(progressUploadBox);

            DeserializeWorkflowSelections(flowDrop);

            return uploadBox;
        }

        private void SerializeWorkflowSelections()
        {
            ASDebug.Log("Serializing workflow selections");
            var json = JsonValue.NewDict();

            // Active workflow
            var activeWorkflow = JsonValue.NewString(_activeWorkflowElement.Name);
            json["ActiveWorkflow"] = activeWorkflow;

            // Workflow Selections
            foreach(var kvp in _uploadWorkflows)
                json[kvp.Key] = kvp.Value.SerializeWorkflow();

            AssetStoreCache.CacheUploadSelections(PackageId, json);
        }

        private void DeserializeWorkflowSelections(ToolbarMenu activeFlowMenu)
        {
            AssetStoreCache.GetCachedUploadSelections(PackageId, out JsonValue cachedSelections);

            // Individual workflow selections
            foreach (var kvp in _uploadWorkflows)
            {
                if (cachedSelections.ContainsKey(kvp.Key))
                    kvp.Value.LoadSerializedWorkflow(cachedSelections[kvp.Key], LastUploadedPath, LastUploadedGuid);
                else
                    kvp.Value.LoadSerializedWorkflowFallback(LastUploadedPath, LastUploadedGuid);
            }

            // Active workflow selection
            if (!cachedSelections.ContainsKey("ActiveWorkflow"))
            {
                // Set default to folder workflow
                SetActiveWorkflowElement(_uploadWorkflows[FolderUploadWorkflowView.WorkflowName], activeFlowMenu);
                return;
            }

            var serializedWorkflow = cachedSelections["ActiveWorkflow"].AsString();
            SetActiveWorkflowElement(_uploadWorkflows[serializedWorkflow], activeFlowMenu);
        }

        private void SetActiveWorkflowElement(UploadWorkflowView newActiveWorkflowElement, ToolbarMenu activeFlowMenu)
        {
            if (_activeWorkflowElement != null)
                _activeWorkflowElement.style.display = DisplayStyle.None;
            
            _activeWorkflowElement = newActiveWorkflowElement;
            _activeWorkflowElement.style.display = DisplayStyle.Flex;
            activeFlowMenu.text = newActiveWorkflowElement.DisplayName;

            UpdateActionButtons();
            SerializeWorkflowSelections();
        }

        private void UpdateActionButtons()
        {
            switch (_activeWorkflowElement)
            {
                case UnityPackageUploadWorkflowView _:
                    _exportButton.style.display = DisplayStyle.None;
                    _uploadButton.style.marginLeft = 0f;
                    _uploadButton.text = "Upload";
                    break;
                default:
                    _exportButton.style.display = DisplayStyle.Flex;
                    _uploadButton.style.marginLeft = 5f;
                    _uploadButton.text = "Export and Upload";
                    break;
            }
        }

        private VisualElement SetupProgressUploadBox()
        {
            var progressUploadBox = new VisualElement();
            progressUploadBox.AddToClassList("progress-upload-box");

            _exportAndUploadContainer = new VisualElement();
            _exportAndUploadContainer.AddToClassList("export-and-upload-container");

            _exportButton = new Button(ExportWithoutUploading) { name = "ExportButton", text = "Export" };
            _exportButton.AddToClassList("export-button");

            _uploadButton = new Button(PreparePackageUpload) { name = "UploadButton", text = "Export and Upload" };
            _uploadButton.AddToClassList("upload-button");

            _exportAndUploadContainer.Add(_exportButton);
            _exportAndUploadContainer.Add(_uploadButton);

            _uploadProgressContainer = new VisualElement();
            _uploadProgressContainer.AddToClassList("upload-progress-container");
            _uploadProgressContainer.style.display = DisplayStyle.None;

            _uploadProgressBarFlow = new ProgressBar { name = "UploadProgressBar" };
            _uploadProgressBarFlow.AddToClassList("upload-progress-bar");
            _uploadProgressFlowBg = _uploadProgressBarFlow.Q<VisualElement>(className: "unity-progress-bar__progress");

            _cancelUploadButton = new Button() { name = "CancelButton", text = "Cancel" };
            _cancelUploadButton.AddToClassList("cancel-button");

            _uploadProgressContainer.Add(_uploadProgressBarFlow);
            _uploadProgressContainer.Add(_cancelUploadButton);

            progressUploadBox.Add(_exportAndUploadContainer);
            progressUploadBox.Add(_uploadProgressContainer);

            return progressUploadBox;
        }

        private string FormatSize(string size)
        {
            if (string.IsNullOrEmpty(size))
                return "0.00 MB";
            
            float.TryParse(size, out var sizeBytes);
            return $"{sizeBytes / (1024f * 1024f):0.00} MB";
        }

        private string FormatDate(string date)
        {
            DateTime dt = DateTime.Parse(date);
            return dt.Date.ToString("yyyy-MM-dd");
        }

        #region Package Uploading

        private async void ExportWithoutUploading()
        {
            var paths = _activeWorkflowElement.GetAllExportPaths();
            if(paths.Length == 0)
            {
                EditorUtility.DisplayDialog("Exporting failed", "No path was selected. Please " +
                    "select a path and try again.", "OK");
                return;
            }

            var rootProjectPath = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
            var packageNameStripped = Regex.Replace(PackageName, "[^a-zA-Z0-9]", "");
            var outputPath = EditorUtility.SaveFilePanel("Export Package", rootProjectPath, 
                $"{packageNameStripped}-{DateTime.Now:yyyy-dd-M--HH-mm-ss}", "unitypackage");

            if (string.IsNullOrEmpty(outputPath))
                return;

            var exportResult = await ExportPackage(outputPath);
            if (!exportResult.Success)
                Debug.LogError($"Package exporting failed: {exportResult.Error}");
            else
                Debug.Log($"Package exported to '{Path.GetFullPath(exportResult.ExportedPath).Replace("\\", "/")}'");
        }

        private async Task<ExportResult> ExportPackage(string outputPath)
        {
            var exportResult = await _activeWorkflowElement.ExportPackage(outputPath, IsCompleteProject);
            return exportResult;
        }

        private bool ValidateUnityVersionsForUpload()
        {
            if (!AssetStoreUploader.ShowPackageVersionDialog)
                return true;

            EditorUtility.DisplayProgressBar("Preparing...", "Checking version compatibility", 0.4f);
            var versions = AssetStoreAPI.GetPackageUploadedVersions(PackageId, VersionId);
            EditorUtility.ClearProgressBar();

            if (versions.Any(x => string.Compare(x, AssetStoreUploader.MinRequiredPackageVersion, StringComparison.Ordinal) >= 0))
                return true;

            var result = EditorUtility.DisplayDialogComplex("Asset Store Tools", $"You may upload this package, but you will need to add a package using Unity version {AssetStoreUploader.MinRequiredPackageVersion} " +
                "or higher to be able to submit a new asset", "Upload", "Cancel", "Upload and do not display this again");

            switch (result)
            {
                case 1:
                    return false;
                case 2:
                    AssetStoreUploader.ShowPackageVersionDialog = false;
                    break;
            }

            return true;
        }

        private async void PreparePackageUpload()
        {
            var paths = _activeWorkflowElement.GetAllExportPaths();
            if (paths.Length == 0)
            {
                EditorUtility.DisplayDialog("Uploading failed", "No path was selected. Please " +
                    "select a path and try again.", "OK");
                return;
            }

            var packageNameStripped = Regex.Replace(PackageName, "[^a-zA-Z0-9]", "");
            var outputPath = $"Temp/{packageNameStripped}-{DateTime.Now:yyyy-dd-M--HH-mm-ss}.unitypackage";
            var exportResult = await ExportPackage(outputPath);
            if (!exportResult.Success)
            {
                Debug.LogError($"Package exporting failed: {exportResult.Error}");
                return;
            }

            if (!ValidateUnityVersionsForUpload())
                return;

            var localPackageGuid = _activeWorkflowElement.GetLocalPackageGuid();
            var localPackagePath = _activeWorkflowElement.GetLocalPackagePath();
            var localProjectPath = _activeWorkflowElement.GetLocalProjectPath();
            BeginPackageUpload(exportResult.ExportedPath, localPackageGuid, localPackagePath, localProjectPath);
        }

        private async void BeginPackageUpload(string exportedPackagePath, string packageGuid, string packagePath, string projectPath)
        {
            // Configure the UI
            // Disable Active Workflow
            EnableWorkflowElements(false);

            // Progress bar
            _exportAndUploadContainer.style.display = DisplayStyle.None;
            _uploadProgressContainer.style.display = DisplayStyle.Flex;

            // Configure the upload cancel button
            _cancelUploadButton.clickable = null;
            _cancelUploadButton.clicked += () => AssetStoreAPI.AbortPackageUpload(VersionId);
            _cancelUploadButton.text = "Cancel";
            
            // Set up upload progress tracking for the unexpanded package progress bar
            EditorApplication.update += OnPackageUploadProgressHeader;

            // Set up upload progress tracking for the expanded package progress bar
            EditorApplication.update += OnPackageUploadProgressContent;
            
            // Set up base analytics data
            var analyticsData = ConstructAnalyticsData(exportedPackagePath);
            
            // Start tracking uploading time
            var watch = System.Diagnostics.Stopwatch.StartNew(); // Debugging
            
            // Start uploading the package
            var result = await AssetStoreAPI.UploadPackageAsync(VersionId, PackageName, exportedPackagePath, packageGuid, packagePath, projectPath);
            
            watch.Stop();
            analyticsData.TimeTaken = watch.Elapsed.TotalSeconds;
            
            switch (result.Status)
            {
                case PackageUploadResult.UploadStatus.Success:
                    analyticsData.UploadFinishedReason = "Success";
                    ASDebug.Log($"Finished uploading, time taken: {watch.Elapsed.TotalSeconds} seconds");
                    await OnPackageUploadSuccess();
                    break;
                case PackageUploadResult.UploadStatus.Cancelled:
                    analyticsData.UploadFinishedReason = "Cancelled";
                    ASDebug.Log($"Uploading cancelled, time taken: {watch.Elapsed.TotalSeconds} seconds");
                    break;
                case PackageUploadResult.UploadStatus.Fail:
                    analyticsData.UploadFinishedReason = result.Error.Exception.ToString();
                    OnPackageUploadFail(result.Error);
                    break;
                case PackageUploadResult.UploadStatus.ResponseTimeout:
                    analyticsData.UploadFinishedReason = "ResponseTimeout";
                    Debug.LogWarning($"All bytes for the package '{PackageName}' have been uploaded, but a response " +
                        $"from the server was not received. This can happen because of Firewall restrictions. " +
                        $"Please make sure that a new version of your package has reached the Publishing Portal.");
                    await OnPackageUploadSuccess();
                    break;
            }

            ASAnalytics.SendUploadingEvent(analyticsData);
            PostUploadCleanup(result.Status);
        }

        private ASAnalytics.AnalyticsData ConstructAnalyticsData(string exportedPackagePath)
        {
            bool validated;
            string validationResults;

            validated = _activeWorkflowElement.GetValidationSummary(out validationResults);

            FileInfo packageFileInfo = new FileInfo(exportedPackagePath);
            string workflow = _activeWorkflowElement.Name;

            ASAnalytics.AnalyticsData data = new ASAnalytics.AnalyticsData
            {
                ToolVersion = AssetStoreAPI.ToolVersion,
                EndpointUrl = AssetStoreAPI.AssetStoreProdUrl,
                PackageId = PackageId,
                Category = Category,
                UsedValidator = validated,
                ValidatorResults = validationResults,
                PackageSize = packageFileInfo.Length,
                Workflow = workflow
            };

            return data;
        }
        
        private void OnPackageUploadProgressHeader()
        {
            // Header progress bar is only shown when the package is not expanded and has progress
            if (_uploadProgressBarHeader.value > 0.0f)
                _uploadProgressBarHeader.style.display = !_expanded ? DisplayStyle.Flex : DisplayStyle.None;
            
            if (!AssetStoreAPI.ActiveUploads.ContainsKey(VersionId))
                return;
            
            _uploadProgressBarHeader.value = AssetStoreAPI.ActiveUploads[VersionId].Progress;
        }

        private void OnPackageUploadProgressContent()
        {
            if (!AssetStoreAPI.ActiveUploads.ContainsKey(VersionId))
                return;

            var progressValue = AssetStoreAPI.ActiveUploads[VersionId].Progress;
            _uploadProgressBarFlow.value = progressValue;
            _uploadProgressBarFlow.title = $"{progressValue:0.#}%";
            
            if(progressValue == 100f && _cancelUploadButton.enabledInHierarchy)
                _cancelUploadButton.SetEnabled(false);
        }

        private async Task OnPackageUploadSuccess()
        {
            if (ASToolsPreferences.Instance.DisplayUploadDialog)
                EditorUtility.DisplayDialog("Success!", $"Package for '{PackageName}' has been uploaded successfully!", "OK");
            
            SetEnabled(false);
            PackageFetcher fetcher = new PackageFetcher();
            var result = await fetcher.FetchRefreshedPackage(PackageId);
            if(!result.Success)
            {
                ASDebug.LogError(result.Error);
                SetEnabled(true);
                return;
            }

            UpdateDataValues(result.Package);
            ASDebug.Log($"Updated name, status, date and size values for package version id {VersionId}");
            SetEnabled(true);
        }

        private void OnPackageUploadFail(ASError error)
        {
            if (ASToolsPreferences.Instance.DisplayUploadDialog)
                EditorUtility.DisplayDialog("Upload failed", "Package uploading failed. See Console for details", "OK");
            
            Debug.LogError(error);
        }

        private void PostUploadCleanup(PackageUploadResult.UploadStatus uploadStatus)
        {
            if (_activeWorkflowElement == null)
                return;

            SetProgressBarColorByStatus(uploadStatus);
            
            _uploadProgressBarFlow.title = $"Upload: {uploadStatus.ToString()}";
            
            _cancelUploadButton.clickable = null;
            _cancelUploadButton.clicked += ResetPostUpload;
            
            _cancelUploadButton.text = "Done";

            // Re-enable the Cancel/Done button since it gets disabled at 100% progress
            _cancelUploadButton.SetEnabled(true);
        }

        private void ResetPostUpload()
        {
            if (_activeWorkflowElement == null)
                return;

            // Cleanup the progress bars
            EditorApplication.update -= OnPackageUploadProgressContent;
            EditorApplication.update -= OnPackageUploadProgressHeader;
            
            EnableWorkflowElements(true);
            ResetProgressBar();
            _exportAndUploadContainer.style.display = DisplayStyle.Flex;
            _uploadProgressContainer.style.display = DisplayStyle.None;
        }

        private void ResetProgressBar()
        {
            SetProgressBarColorByStatus(PackageUploadResult.UploadStatus.Default);
            
            _uploadProgressBarHeader.style.display = DisplayStyle.None;
            _uploadProgressBarHeader.value = 0f;

            _uploadProgressBarFlow.value = 0f;
            _uploadProgressBarFlow.title = string.Empty;
        }

        private void EnableWorkflowElements(bool enable)
        {
            _workflowSelectionBox?.SetEnabled(enable);
            _activeWorkflowElement?.SetEnabled(enable);
        }
        
        private void SetProgressBarColorByStatus(PackageUploadResult.UploadStatus status)
        {
            var color = PackageUploadResult.GetColorByStatus(status);

            _uploadProgressFlowBg.style.backgroundColor = color;
            _uploadProgressHeaderBg.style.backgroundColor = color;
        }

#endregion
    }
}