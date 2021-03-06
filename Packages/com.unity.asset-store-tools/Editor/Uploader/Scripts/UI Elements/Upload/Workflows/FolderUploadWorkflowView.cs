using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AssetStoreTools.Exporter;
using AssetStoreTools.Utility;
using AssetStoreTools.Utility.Json;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssetStoreTools.Uploader.UIElements
{
    internal class FolderUploadWorkflowView : UploadWorkflowView
    {
        public const string WorkflowName = "FolderWorkflow";
        public const string WorkflowDisplayName = "From Assets Folder";

        public override string Name => WorkflowName;
        public override string DisplayName => WorkflowDisplayName;

        private Toggle _dependenciesToggle;
        private List<string> _includedDependencies = new List<string>();

        private bool _isCompleteProject;

        private VisualElement _specialFolderTogglesBox;
        private VisualElement _specialFoldersElement;
        
        private VisualElement _packageDependencyBox;
        private ScrollView _packagesTogglesBox;

        private ToolbarMenu _filteringDropdown;
        private Label _noPackagesLabel;
        private string _packagesFilter;
        
        // Special folders that would not work if not placed directly in the 'Assets' folder
        private readonly string[] _extraAssetFolderNames =
        {
            "Editor Default Resources", "Gizmos", "Plugins",
            "StreamingAssets", "Standard Assets", "WebGLTemplates",
            "ExternalDependencyManager", "XR"
        };

        private readonly List<string> _packageSelectionFilters = new List<string> { "All", "Selected", "Not Selected" };

        private FolderUploadWorkflowView(string category, bool isCompleteProject, Action serializeSelection) : base(serializeSelection)
        {
            _isCompleteProject = isCompleteProject;
            Category = category;
            
            SetupWorkflow();
        }

        public static FolderUploadWorkflowView Create(string category, bool isCompleteProject, Action serializeAction)
        {
            return new FolderUploadWorkflowView(category, isCompleteProject, serializeAction);
        }

        public void SetCompleteProject(bool isCompleteProject)
        {
            _isCompleteProject = isCompleteProject;
        }

        private bool GetIncludeDependenciesToggle()
        {
            return _dependenciesToggle.value;
        }

        private List<string> GetIncludedDependencies()
        {
            return _includedDependencies;
        }
        
        protected sealed override void SetupWorkflow()
        {
            // Path selection
            VisualElement folderPathSelectionRow = new VisualElement();
            folderPathSelectionRow.AddToClassList("selection-box-row");

            VisualElement labelHelpRow = new VisualElement();
            labelHelpRow.AddToClassList("label-help-row");

            Label folderPathLabel = new Label { text = "Folder path" };
            Image folderPathLabelTooltip = new Image
            {
                tooltip = "Select the main folder of your package" +
                "\n\nAll files and folders of your package should preferably be contained within a single root folder that is named after your package" +
                "\n\nExample: 'Assets/[MyPackageName]'" +
                "\n\nNote: If your content makes use of special folders that are required to be placed in the root Assets folder (e.g. 'StreamingAssets')," +
                " you will be able to include them after selecting the main folder"
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

            // Dependencies selection
            VisualElement dependenciesSelectionRow = new VisualElement();
            dependenciesSelectionRow.AddToClassList("selection-box-row");

            VisualElement dependenciesLabelHelpRow = new VisualElement();
            dependenciesLabelHelpRow.AddToClassList("label-help-row");

            Label dependenciesLabel = new Label { text = "Dependencies" };
            Image dependenciesLabelTooltip = new Image
            {
                tooltip = "Tick this checkbox if your package content has dependencies on Unity packages from the Package Manager"
            };

            _dependenciesToggle = new Toggle { name = "DependenciesToggle", text = "Include Package Manifest" };
            _dependenciesToggle.AddToClassList("dependencies-toggle");
            
            _dependenciesToggle.RegisterValueChangedCallback((_) => SerializeSelection?.Invoke());
            _dependenciesToggle.RegisterValueChangedCallback(OnDependencyToggleValueChange);
            
            RegisterCallback<AttachToPanelEvent>((_) => {ASToolsPreferences.OnSettingsChange += OnASTSettingsChange;});
            RegisterCallback<DetachFromPanelEvent>((_) => {ASToolsPreferences.OnSettingsChange -= OnASTSettingsChange;});
            
            // Dependencies selection
            _packageDependencyBox = new VisualElement();
            _packageDependencyBox.AddToClassList("selection-box-row");
            _packageDependencyBox.style.display = DisplayStyle.None;

            dependenciesLabelHelpRow.Add(dependenciesLabel);
            dependenciesLabelHelpRow.Add(dependenciesLabelTooltip);

            dependenciesSelectionRow.Add(dependenciesLabelHelpRow);
            dependenciesSelectionRow.Add(_dependenciesToggle);

            Add(dependenciesSelectionRow);
            Add(_packageDependencyBox);

            ValidationElement = new AssetValidationElement();
            Add(ValidationElement);
            
            ValidationElement.SetCategory(Category);
        }

        public override JsonValue SerializeWorkflow()
        {
            var workflowDict = base.SerializeWorkflow();
            workflowDict["dependencies"] = GetIncludeDependenciesToggle();
            workflowDict["dependenciesNames"] = GetIncludedDependencies().Select(JsonValue.NewString).ToList();

            return workflowDict;
        }

        public override void LoadSerializedWorkflow(JsonValue json, string lastUploadedPath, string lastUploadedGuid)
        {
            if (!DeserializeMainExportPath(json, out string mainExportPath) || (!Directory.Exists(mainExportPath) && mainExportPath != String.Empty))
            {
                ASDebug.Log("Unable to restore Folder upload workflow paths from the local cache");
                LoadSerializedWorkflowFallback(lastUploadedPath, lastUploadedGuid);
                return;
            }

            DeserializeExtraExportPaths(json, out List<string> extraExportPaths);
            DeserializeDependencies(json, out List<string> dependencies);
            DeserializeDependenciesToggle(json, out var dependenciesToggle);

            ASDebug.Log($"Restoring serialized Folder workflow values from local cache");
            HandleFolderUploadPathSelection(mainExportPath, extraExportPaths, dependencies, false);
            
            if (dependenciesToggle)
            {
                _dependenciesToggle.SetValueWithoutNotify(true);
                FindAndPopulateDependencies(_includedDependencies);
            }
        }

        public override void LoadSerializedWorkflowFallback(string lastUploadedPath, string lastUploadedGuid)
        {
            var mainExportPath = AssetDatabase.GUIDToAssetPath(lastUploadedGuid);
            if (string.IsNullOrEmpty(mainExportPath))
                mainExportPath = lastUploadedPath;

            if ((!mainExportPath.StartsWith("Assets/") && mainExportPath != "Assets") || !Directory.Exists(mainExportPath))
            {
                ASDebug.Log("Unable to restore Folder workflow paths from previous upload values");
                return;
            }

            ASDebug.Log($"Restoring serialized Folder workflow values from previous upload values");
            HandleFolderUploadPathSelection(mainExportPath, null, null, false);
        }

        #region Folder Upload

        protected override void BrowsePath()
        {
            // Path retrieval
            var absoluteExportPath = string.Empty;
            var relativeExportPath = string.Empty;
            var rootProjectPath = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);

            bool includeAllAssets = false;

            if (_isCompleteProject)
            {
                includeAllAssets = EditorUtility.DisplayDialog("Notice",
                    "Your package draft is set to a category that is treated" +
                    " as a complete project. Project settings will be included automatically. Would you like everything in the " +
                    "'Assets' folder to be included?\n\nYou will still be able to change the selected assets before uploading",
                    "Yes, include all folders and assets",
                    "No, I'll select what to include manually");
                if (includeAllAssets)
                    absoluteExportPath = Application.dataPath;
            }

            if (!includeAllAssets)
            {
                absoluteExportPath =
                    EditorUtility.OpenFolderPanel("Select folder to compress into a package", "Assets/", "");
                if (string.IsNullOrEmpty(absoluteExportPath))
                    return;
            }
            
            if (absoluteExportPath.StartsWith(rootProjectPath))
            {
                relativeExportPath = absoluteExportPath.Substring(rootProjectPath.Length);
            }
            else
            {
                if (ASToolsPreferences.Instance.EnableSymlinkSupport)
                    SymlinkUtil.FindSymlinkFolderRelative(absoluteExportPath, out relativeExportPath);
            }

            if (!relativeExportPath.StartsWith("Assets/") && !(relativeExportPath == "Assets" && _isCompleteProject))
            {
                if (relativeExportPath.StartsWith("Assets") && !_isCompleteProject)
                    EditorUtility.DisplayDialog("Invalid selection",
                        "'Assets' folder is only available for packages tagged as a 'Complete Project'.", "OK");
                else
                    EditorUtility.DisplayDialog("Invalid selection", "Selected folder path must be within the project.",
                        "OK");
                return;
            }

            HandleFolderUploadPathSelection(relativeExportPath, null, _includedDependencies, true);
        }

        private void HandleFolderUploadPathSelection(string relativeExportPath, List<string> serializedToggles, List<string> dependencies, bool serializeValues)
        {
            if (relativeExportPath != String.Empty)
                PathSelectionField.value = relativeExportPath + "/";

            MainExportPath = relativeExportPath;
            ExtraExportPaths = new List<string>();
            _includedDependencies = new List<string>();

            LocalPackageGuid = AssetDatabase.AssetPathToGUID(MainExportPath);
            LocalPackagePath = MainExportPath;
            LocalProjectPath = MainExportPath;

            if (_specialFoldersElement != null)
            {
                _specialFoldersElement.Clear();
                Remove(_specialFoldersElement);

                _specialFoldersElement = null;
            }

            // Prompt additional path selection (e.g. StreamingAssets, WebGLTemplates, etc.)
            List<string> specialFoldersFound = new List<string>();

            foreach (var extraAssetFolderName in _extraAssetFolderNames)
            {
                var fullExtraPath = "Assets/" + extraAssetFolderName;

                if (!Directory.Exists(fullExtraPath))
                    continue;

                if (MainExportPath.ToLower().StartsWith(fullExtraPath.ToLower()))
                    continue;

                // Don't include nested paths
                if (!fullExtraPath.ToLower().StartsWith(MainExportPath.ToLower()))
                    specialFoldersFound.Add(fullExtraPath);
            }

            if (specialFoldersFound.Count != 0)
                PopulateSpecialFoldersBox(specialFoldersFound, serializedToggles);
            
            if (dependencies != null && dependencies.Count != 0)
                FindAndPopulateDependencies(dependencies);

            // After setting up the main and extra paths update validation paths
            UpdateValidationPaths();

            // Only serialize current selection when no serialized toggles were passed
            if (serializeValues)
                SerializeSelection?.Invoke();
        }

        private void InitializeSpecialFoldersSelection()
        {
            // Dependencies selection
            _specialFoldersElement = new VisualElement();
            _specialFoldersElement.AddToClassList("selection-box-row");

            VisualElement specialFoldersHelpRow = new VisualElement();
            specialFoldersHelpRow.AddToClassList("label-help-row");

            Label specialFoldersLabel = new Label { text = "Special folders" };
            Image specialFoldersLabelTooltip = new Image
            {
                tooltip =
                    "If your package content relies on Special Folders (e.g. StreamingAssets), please select which of these folders should be included in the package"
            };

            _specialFolderTogglesBox = new VisualElement { name = "SpecialFolderToggles" };
            _specialFolderTogglesBox.AddToClassList("special-folders-toggles-box");

            specialFoldersHelpRow.Add(specialFoldersLabel);
            specialFoldersHelpRow.Add(specialFoldersLabelTooltip);

            _specialFoldersElement.Add(specialFoldersHelpRow);
            _specialFoldersElement.Add(_specialFolderTogglesBox);

            
            Add(_specialFoldersElement);
        }

        private void PopulateSpecialFoldersBox(List<string> specialFoldersFound, List<string> checkedToggles)
        {
            InitializeSpecialFoldersSelection();
            
            EventCallback<ChangeEvent<bool>, string> toggleChangeCallback = OnSpecialFolderPathToggledAsset;

            foreach (var path in specialFoldersFound)
            {
                var toggle = new Toggle { value = false, text = path };
                toggle.AddToClassList("special-folder-toggle");
                if (checkedToggles != null && checkedToggles.Contains(toggle.text))
                {
                    toggle.SetValueWithoutNotify(true);
                    ExtraExportPaths.Add(toggle.text);
                }

                toggle.RegisterCallback(toggleChangeCallback, toggle.text);
                _specialFolderTogglesBox.Add(toggle);
            }
        }
        
        private void OnSpecialFolderPathToggledAsset(ChangeEvent<bool> evt, string folderPath)
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

            UpdateValidationPaths();
            SerializeSelection?.Invoke();
        }

        private void UpdateValidationPaths()
        {
            var validationPaths = new List<string>() { MainExportPath };
            validationPaths.AddRange(ExtraExportPaths);
            ValidationElement.SetValidationPaths(validationPaths.ToArray());
        }
        
        private void OnToggleDependency(ChangeEvent<bool> evt, string dependency)
        {
            switch (evt.newValue)
            {
                case true when !_includedDependencies.Contains(dependency):
                    _includedDependencies.Add(dependency);
                    break;
                case false when _includedDependencies.Contains(dependency):
                    _includedDependencies.Remove(dependency);
                    break;
            }

            FilterPackageSelection(_packagesFilter);
            SerializeSelection?.Invoke();
        }

        private void OnDependencyToggleValueChange(ChangeEvent<bool> evt)
        {
            CheckDependencyBoxState();
        }

        private void OnASTSettingsChange()
        {
            CheckDependencyBoxState();
        }

        private void CheckDependencyBoxState()
        {
            if (_dependenciesToggle.value && !ASToolsPreferences.Instance.UseLegacyExporting)
            {
                FindAndPopulateDependencies(_includedDependencies);
            }
            else
            {
                _packageDependencyBox.style.display = DisplayStyle.None;
            }
        }

        private void FindAndPopulateDependencies(List<string> checkedToggles)
        {
            _packageDependencyBox?.Clear();
            var registryPackages = PackageUtility.GetAllRegistryPackages();

            if (registryPackages == null)
            {
                ASDebug.LogWarning("Package Manifest was not found or could not be parsed.");
                return;
            }

            List<string> packagesFound = new List<string>(registryPackages.Select(x => x.name));
            PopulatePackagesSelectionBox(packagesFound, checkedToggles);
        }
        
        private void PopulatePackagesSelectionBox(List<string> packagesFound, List<string> checkedToggles)
        {
            InitializePackageSelection();
            
            EventCallback<ChangeEvent<bool>, string> toggleChangeCallback = OnToggleDependency;

            if (packagesFound.Count == 0 || ASToolsPreferences.Instance.UseLegacyExporting)
            {
                _packageDependencyBox.style.display = DisplayStyle.None;
                return;
            }
            
            _packageDependencyBox.style.display = DisplayStyle.Flex;

            foreach (var path in packagesFound)
            {
                var toggle = new Toggle { value = false, text = path };
                toggle.AddToClassList("extra-packages-toggle");
                if (checkedToggles != null && checkedToggles.Contains(toggle.text))
                {
                    toggle.SetValueWithoutNotify(true);
                    
                    if (!_includedDependencies.Contains(toggle.text))
                        _includedDependencies.Add(toggle.text);
                }

                toggle.RegisterCallback(toggleChangeCallback, toggle.text);
                _packagesTogglesBox.Add(toggle);
            }
        }

        private void InitializePackageSelection()
        {
            VisualElement dependenciesHelpRow = new VisualElement();
            dependenciesHelpRow.AddToClassList("label-help-row");

            Label allPackagesLabel = new Label { text = "All Packages" };
            Image allPackagesLabelTooltip = new Image
            {
                tooltip =
                    "Select UPM dependencies you would like to include with your package."
            };

            VisualElement fullPackageSelectionBox = new VisualElement();
            fullPackageSelectionBox.AddToClassList("extra-packages-box");

            _packagesTogglesBox = new ScrollView { name = "DependencyToggles" };
            _packagesTogglesBox.AddToClassList("extra-packages-scroll-view");

            _noPackagesLabel = new Label("No packages were found that match this criteria.");
            _noPackagesLabel.AddToClassList("no-packages-label");
            
            var scrollContainer = _packagesTogglesBox.Q<VisualElement>("unity-content-viewport");
            scrollContainer.Add(_noPackagesLabel);

            VisualElement packagesFilteringBox = new VisualElement();
            packagesFilteringBox.AddToClassList("packages-filtering-box");

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

            _filteringDropdown = new ToolbarMenu {text = _packagesFilter = _packageSelectionFilters[0]};
            _filteringDropdown.AddToClassList("filter-packages-dropdown");

            foreach (var filter in _packageSelectionFilters)
                _filteringDropdown.menu.AppendAction(filter, delegate { FilterPackageSelection(filter);});

            filteringDropdownBox.Add(_filteringDropdown);

            VisualElement packageSelectionButtonsBox = new VisualElement();
            packageSelectionButtonsBox.AddToClassList("extra-packages-buttons-box");
            
            // Final adding
            packagesFilteringBox.Add(filteringDropdownBox);
            packagesFilteringBox.Add(selectingPackagesBox);
            
            fullPackageSelectionBox.Add(_packagesTogglesBox);
            fullPackageSelectionBox.Add(packagesFilteringBox);
            
            dependenciesHelpRow.Add(allPackagesLabel);
            dependenciesHelpRow.Add(allPackagesLabelTooltip);

            _packageDependencyBox.Add(dependenciesHelpRow);
            _packageDependencyBox.Add(fullPackageSelectionBox);
        }

        private void FilterPackageSelection(string filter)
        {
            var allToggles = _packagesTogglesBox.Children().Cast<Toggle>().ToArray();
            var selectedIndex = _packageSelectionFilters.FindIndex(x => x == filter);
            
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
            _noPackagesLabel.style.display = count > 0 ? DisplayStyle.None : DisplayStyle.Flex;
            
            _packagesFilter = filter;
            _filteringDropdown.text = filter;
        }

        private void SelectAllPackages(bool shouldSelect)
        {
            var allToggles = _packagesTogglesBox.Children().Cast<Toggle>();

            foreach (var toggle in allToggles)
                toggle.value = shouldSelect;
        }

        public override async Task<ExportResult> ExportPackage(string outputPath, bool isCompleteProject)
        {
            var paths = GetAllExportPaths();
            if (isCompleteProject)
                paths = IncludeProjectSettings(paths);

            var includeDependencies = GetIncludeDependenciesToggle();

            var dependenciesToInclude = Array.Empty<string>();

            if (includeDependencies)
                dependenciesToInclude = GetIncludedDependencies().ToArray();

            ExporterSettings exportSettings;

            if (ASToolsPreferences.Instance.UseLegacyExporting)
                exportSettings = new LegacyExporterSettings()
                {
                    ExportPaths = paths,
                    OutputFilename = outputPath,
                    IncludeDependencies = includeDependencies,
                };
            else
                exportSettings = new DefaultExporterSettings()
                {
                    ExportPaths = paths,
                    OutputFilename = outputPath,
                    Dependencies = dependenciesToInclude,
                };

            return await PackageExporter.ExportPackage(exportSettings);
        }

        private string[] IncludeProjectSettings(string[] exportPaths)
        {
            if (exportPaths.Contains("ProjectSettings"))
                return exportPaths;

            var updatedExportPaths = new string[exportPaths.Length + 1];
            exportPaths.CopyTo(updatedExportPaths, 0);
            updatedExportPaths[updatedExportPaths.Length - 1] = "ProjectSettings";

            return updatedExportPaths;
        }

        #endregion
    }
}