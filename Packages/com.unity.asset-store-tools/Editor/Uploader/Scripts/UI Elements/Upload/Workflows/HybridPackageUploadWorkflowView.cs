using AssetStoreTools.Exporter;
using AssetStoreTools.Utility;
using AssetStoreTools.Utility.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace AssetStoreTools.Uploader.UIElements
{
    internal class HybridPackageUploadWorkflowView : UploadWorkflowView
    {
        public const string WorkflowName = "HybridPackageWorkflow";
        public const string WorkflowDisplayName = "Local UPM Package";

        public override string Name => WorkflowName;
        public override string DisplayName => WorkflowDisplayName;

        private VisualElement _extraPackagesBox;
        private Label _noExtraPackagesLabel;
        private ScrollView _extraPackagesTogglesBox;

        private ToolbarMenu _filteringDropdown;
        private string _extraPackagesFilter;
        private readonly List<string> _extraPackageSelectionFilters = new List<string> { "All", "Selected", "Not Selected" };

        private HybridPackageUploadWorkflowView(string category, Action serializeSelection) : base(serializeSelection)
        {
            Category = category;
            
            SetupWorkflow();
        }

        public static HybridPackageUploadWorkflowView Create(string category, Action serializeAction)
        {
            return new HybridPackageUploadWorkflowView(category, serializeAction);
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
                tooltip = "Select a local Package you would like to export and upload to the Store."
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

            ValidationElement = new AssetValidationElement();
            Add(ValidationElement);
            
            ValidationElement.SetCategory(Category);
        }

        public override void LoadSerializedWorkflow(JsonValue json, string lastUploadedPath, string lastUploadedGuid)
        {
            if(!DeserializeMainExportPath(json, out string mainExportPath) || !Directory.Exists(mainExportPath))
            {
                ASDebug.Log("Unable to restore Hybrid Package workflow paths from local cache");
                LoadSerializedWorkflowFallback(lastUploadedGuid, lastUploadedGuid);
                return;
            }

            DeserializeExtraExportPaths(json, out List<string> extraExportPaths);

            ASDebug.Log($"Restoring serialized Hybrid Package workflow values from local cache");
            LoadSerializedWorkflow(mainExportPath, extraExportPaths);
        }

        public override void LoadSerializedWorkflowFallback(string lastUploadedPath, string lastUploadedGuid)
        {
            var mainExportPath = AssetDatabase.GUIDToAssetPath(lastUploadedGuid);
            if (string.IsNullOrEmpty(mainExportPath))
                mainExportPath = lastUploadedPath;
            
            if (!mainExportPath.StartsWith("Packages/") || !Directory.Exists(mainExportPath))
            {
                ASDebug.Log("Unable to restore Hybrid Package workflow paths from previous upload values");
                return;
            }

            ASDebug.Log($"Restoring serialized Hybrid Package workflow values from previous upload values");
            LoadSerializedWorkflow(mainExportPath, null);
        }

        private void LoadSerializedWorkflow(string relativeAssetDatabasePath, List<string> extraExportPaths)
        {
            // Expected path is in ADB form, so we need to reconstruct it first
            var rootProjectPath = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
            var realPath = Path.GetFullPath(relativeAssetDatabasePath).Replace('\\', '/');
            if (realPath.StartsWith(rootProjectPath))
                realPath = realPath.Substring(rootProjectPath.Length);

            if (!IsValidLocalPackage(realPath, out relativeAssetDatabasePath))
            {
                ASDebug.Log("Unable to restore Hybrid Package workflow path - package is not a valid UPM package");
                return;
            }

            // Treat this as a manual selection
            HandleHybridUploadPathSelection(realPath, relativeAssetDatabasePath, extraExportPaths, false);
        }

        protected override void BrowsePath()
        {
            // Path retrieval
            string relativeExportPath = string.Empty;
            string rootProjectPath = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);

            var absoluteExportPath = EditorUtility.OpenFolderPanel("Select the Package", "Packages/", "");

            if (string.IsNullOrEmpty(absoluteExportPath))
                return;

            if (absoluteExportPath.StartsWith(rootProjectPath))
                relativeExportPath = absoluteExportPath.Substring(rootProjectPath.Length);

            var workingPath = !string.IsNullOrEmpty(relativeExportPath) ? relativeExportPath : absoluteExportPath;
            if (!IsValidLocalPackage(workingPath, out string relativeAssetDatabasePath))
            {
                EditorUtility.DisplayDialog("Invalid selection", "Selected export path must be a valid local package", "OK");
                return;
            }

            HandleHybridUploadPathSelection(workingPath, relativeAssetDatabasePath, null, true);
        }

        private void HandleHybridUploadPathSelection(string relativeExportPath, string relativeAssetDatabasePath, List<string> serializedToggles, bool serializeValues)
        {
            PathSelectionField.value = relativeExportPath + "/";

            // Reset and reinitialize the selected export path(s) array
            MainExportPath = relativeAssetDatabasePath;
            ExtraExportPaths = new List<string>();

            // Set additional upload data for the Publisher Portal backend (GUID and Package Path).
            // The backend workflow currently accepts only 1 package guid and path, so we'll use the main folder data
            LocalPackageGuid = AssetDatabase.AssetPathToGUID(relativeAssetDatabasePath);
            LocalPackagePath = relativeAssetDatabasePath;
            LocalProjectPath = relativeAssetDatabasePath;

            if (_extraPackagesBox != null)
            {
                _extraPackagesBox.Clear();
                Remove(_extraPackagesBox);

                _extraPackagesBox = null;
            }

            List<string> pathsToAdd = new List<string>();
            foreach (var package in PackageUtility.GetAllLocalPackages())
            {
                // Exclude the Asset Store Tools themselves
                if (package.name == "com.unity.asset-store-tools")
                    continue;

                var localPackagePath = package.GetConvenientPath();

                if (localPackagePath == relativeExportPath)
                    continue;

                pathsToAdd.Add(package.assetPath);
            }

            pathsToAdd.Sort();

            if (pathsToAdd.Count != 0)
                PopulateExtraPackagesBox(pathsToAdd, serializedToggles);

            // After setting up the main and extra paths update validation paths
            UpdateValidationPaths();

            if (serializeValues)
                SerializeSelection?.Invoke();
        }

        private void PopulateExtraPackagesBox(List<string> otherPackagesFound, List<string> checkedToggles)
        {
            // Dependencies selection
            _extraPackagesBox = new VisualElement();
            _extraPackagesBox.AddToClassList("selection-box-row");

            InitializeExtraPackageSelection();

            EventCallback<ChangeEvent<bool>, string> toggleChangeCallback = OnToggledPackage;

            foreach (var path in otherPackagesFound)
            {
                var toggle = new Toggle { value = false, text = path };
                toggle.AddToClassList("extra-packages-toggle");
                toggle.tooltip = path;
                if (checkedToggles != null && checkedToggles.Contains(toggle.text))
                {
                    toggle.SetValueWithoutNotify(true);
                    ExtraExportPaths.Add(toggle.text);
                }

                toggle.RegisterCallback(toggleChangeCallback, toggle.text);
                _extraPackagesTogglesBox.Add(toggle);
            }

            Add(_extraPackagesBox);
        }

        private void InitializeExtraPackageSelection()
        {
            VisualElement extraPackagesHelpRow = new VisualElement();
            extraPackagesHelpRow.AddToClassList("label-help-row");

            Label extraPackagesLabel = new Label { text = "Extra Packages" };
            Image extraPackagesLabelTooltip = new Image
            {
                tooltip = "If your package has dependencies on other local packages, please select which of these packages should also be included in the resulting package"
            };

            var fullPackageSelectionBox = new VisualElement();
            fullPackageSelectionBox.AddToClassList("extra-packages-box");

            _extraPackagesTogglesBox = new ScrollView { name = "ExtraPackageToggles" };
            _extraPackagesTogglesBox.AddToClassList("extra-packages-scroll-view");

            _noExtraPackagesLabel = new Label("No packages were found that match this criteria.");
            _noExtraPackagesLabel.AddToClassList("no-packages-label");

            var scrollContainer = _extraPackagesTogglesBox.Q<VisualElement>("unity-content-viewport");
            scrollContainer.Add(_noExtraPackagesLabel);

            VisualElement extraPackagesFilteringBox = new VisualElement();
            extraPackagesFilteringBox.AddToClassList("packages-filtering-box");

            // Select - deselect buttons
            VisualElement selectingPackagesBox = new VisualElement();
            selectingPackagesBox.AddToClassList("filtering-packages-buttons-box");

            Button selectAllButton = new Button(() => SelectAllPackages(true))
            {
                text = "Select All"
            };

            Button deSelectAllButton = new Button(() => SelectAllPackages(false))
            {
                text = "Deselect All"
            };

            selectAllButton.AddToClassList("filter-packages-button");
            deSelectAllButton.AddToClassList("filter-packages-button");

            selectingPackagesBox.Add(selectAllButton);
            selectingPackagesBox.Add(deSelectAllButton);

            // Filtering dropdown
            VisualElement filteringDropdownBox = new VisualElement();
            filteringDropdownBox.AddToClassList("filtering-packages-dropdown-box");

            _filteringDropdown = new ToolbarMenu { text = _extraPackagesFilter = _extraPackageSelectionFilters[0] };
            _filteringDropdown.AddToClassList("filter-packages-dropdown");

            foreach (var filter in _extraPackageSelectionFilters)
                _filteringDropdown.menu.AppendAction(filter, delegate { FilterPackageSelection(filter); });

            filteringDropdownBox.Add(_filteringDropdown);

            VisualElement packageSelectionButtonsBox = new VisualElement();
            packageSelectionButtonsBox.AddToClassList("extra-packages-buttons-box");

            // Final adding
            extraPackagesFilteringBox.Add(filteringDropdownBox);
            extraPackagesFilteringBox.Add(selectingPackagesBox);

            fullPackageSelectionBox.Add(_extraPackagesTogglesBox);
            fullPackageSelectionBox.Add(extraPackagesFilteringBox);

            extraPackagesHelpRow.Add(extraPackagesLabel);
            extraPackagesHelpRow.Add(extraPackagesLabelTooltip);

            _extraPackagesBox.Add(extraPackagesHelpRow);
            _extraPackagesBox.Add(fullPackageSelectionBox);
        }

        private void SelectAllPackages(bool shouldSelect)
        {
            var allToggles = _extraPackagesTogglesBox.Children().Cast<Toggle>();

            foreach (var toggle in allToggles)
                toggle.value = shouldSelect;
        }

        private void FilterPackageSelection(string filter)
        {
            var allToggles = _extraPackagesTogglesBox.Children().Cast<Toggle>().ToArray();
            var selectedIndex = _extraPackageSelectionFilters.FindIndex(x => x == filter);

            switch (selectedIndex)
            {
                case 0:
                    foreach (var toggle in allToggles)
                        toggle.style.display = DisplayStyle.Flex;
                    break;
                case 1:
                    foreach (var toggle in allToggles)
                        toggle.style.display = toggle.value ? DisplayStyle.Flex : DisplayStyle.None;
                    break;
                case 2:
                    foreach (var toggle in allToggles)
                        toggle.style.display = toggle.value ? DisplayStyle.None : DisplayStyle.Flex;
                    break;
            }

            // Check if any toggles are displayed
            var count = allToggles.Count(toggle => toggle.style.display == DisplayStyle.Flex);
            _noExtraPackagesLabel.style.display = count > 0 ? DisplayStyle.None : DisplayStyle.Flex;

            _extraPackagesFilter = filter;
            _filteringDropdown.text = filter;
        }

        private void OnToggledPackage(ChangeEvent<bool> evt, string folderPath)
        {
            switch (evt.newValue)
            {
                case true when !ExtraExportPaths.Contains(folderPath):
                    ExtraExportPaths.Add(folderPath);
                    break;
                case false when ExtraExportPaths.Contains(folderPath):
                    ExtraExportPaths.Remove(folderPath);
                    break;
            }

            FilterPackageSelection(_extraPackagesFilter);
            UpdateValidationPaths();
            SerializeSelection?.Invoke();
        }

        private void UpdateValidationPaths()
        {
            var validationPaths = new List<string>() { MainExportPath };
            validationPaths.AddRange(ExtraExportPaths);
            ValidationElement.SetValidationPaths(validationPaths.ToArray());
        }

        private bool IsValidLocalPackage(string packageFolderPath, out string assetDatabasePackagePath)
        {
            assetDatabasePackagePath = string.Empty;

            string packageManifestPath = $"{packageFolderPath}/package.json";

            if (!File.Exists(packageManifestPath))
                return false;
            try
            {
                var localPackages = PackageUtility.GetAllLocalPackages();

                if (localPackages == null || localPackages.Length == 0)
                    return false;

                foreach (var package in localPackages)
                {
                    var localPackagePath = package.GetConvenientPath();

                    if (localPackagePath != packageFolderPath)
                        continue;

                    assetDatabasePackagePath = package.assetPath;
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        public override async Task<ExportResult> ExportPackage(string outputPath, bool _)
        {
            var paths = GetAllExportPaths();

            var exportSettings = new DefaultExporterSettings()
            {
                ExportPaths = paths,
                OutputFilename = outputPath
            };

            return await PackageExporter.ExportPackage(exportSettings);
        }
    }
}