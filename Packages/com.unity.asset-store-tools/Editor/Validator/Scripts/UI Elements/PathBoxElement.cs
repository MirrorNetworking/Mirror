using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetStoreTools.Utility;
using AssetStoreTools.Validator.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssetStoreTools.Validator.UIElements
{
    internal class PathBoxElement : VisualElement
    {
        private VisualElement _pathSelectionColumn;
        private List<Label> _validationPaths;
        private ScrollView _pathBoxScrollView;
        
        public PathBoxElement()
        {
            ConstructPathBox();
        }

        public List<string> GetValidationPaths()
        {
            return FilterValidationPaths();
        }

        private List<string> FilterValidationPaths()
        {
            var filteredPaths = new List<string>();
            foreach(var textField in _validationPaths)
            {
                var path = textField.text;

                // Exclude empty paths
                if (string.IsNullOrEmpty(path))
                    continue;

                // Exclude hidden paths
                if(path.Split('/').Any(x => x.EndsWith("~")))
                {
                    Debug.LogWarning($"Path '{path}' cannot be validated as it is a hidden folder and not part of the Asset Database");
                    continue;
                }

                // Exclude paths that were serialized during previous validations, but no longer exist
                if(!Directory.Exists(path))
                {
                    Debug.LogWarning($"Path '{path}' cannot be validated because it no longer exists");
                    continue;
                }

                // Prevent redundancy for new paths
                if (filteredPaths.Any(x => path.StartsWith(x + "/")))
                    continue;

                // Prevent redundancy for already added paths
                var redundantPaths = filteredPaths.Where(x => x.StartsWith(path + "/")).ToArray();
                foreach (var redundantPath in redundantPaths)
                    filteredPaths.Remove(redundantPath);

                filteredPaths.Add(path);
            }

            return filteredPaths;
        }

        private void ConstructPathBox()
        {
            AddToClassList("path-box");

            _pathSelectionColumn = new VisualElement();
            _pathSelectionColumn.AddToClassList("selection-box-column");

            var pathSelectionRow = new VisualElement();
            pathSelectionRow.AddToClassList("selection-box-row");

            VisualElement labelHelpRow = new VisualElement();
            labelHelpRow.AddToClassList("label-help-row");
            labelHelpRow.style.alignSelf = Align.FlexStart;

            Label pathLabel = new Label { text = "Validation paths" };
            Image pathLabelTooltip = new Image
            {
                tooltip = "Select the folder (or multiple folders) that your package consists of." +
                          "\n\nAll files and folders of your package should be contained within " +
                          "a single root folder that is named after your package " +
                          "(e.g. 'Assets/[MyPackageName]' or 'Packages/[MyPackageName]')" +
                          "\n\nIf your package includes special folders that cannot be nested within " +
                          "the root package folder (e.g. 'WebGLTemplates'), they should be added to this list " +
                          "together with the root package folder"
            };

            labelHelpRow.Add(pathLabel);
            labelHelpRow.Add(pathLabelTooltip);

            _validationPaths = new List<Label>();
            var serializedValidationState = ValidationState.Instance.ValidationStateData;

            var fullPathBox = new VisualElement() { name = "ValidationPaths" };
            fullPathBox.AddToClassList("validation-paths-box");

            _pathBoxScrollView = new ScrollView { name = "ValidationPathsScrollView" };
            _pathBoxScrollView.AddToClassList("validation-paths-scroll-view");
            
            VisualElement scrollViewBottomRow = new VisualElement();
            scrollViewBottomRow.AddToClassList("validation-paths-scroll-view-bottom-row");

            var addExtraPathsButton = new Button(Browse) { text = "Add a path" };
            addExtraPathsButton.AddToClassList("validation-path-add-button");
            scrollViewBottomRow.Add(addExtraPathsButton);

            fullPathBox.Add(_pathBoxScrollView);
            fullPathBox.Add(scrollViewBottomRow);

            pathSelectionRow.Add(labelHelpRow);
            pathSelectionRow.Add(fullPathBox);

            _pathSelectionColumn.Add(pathSelectionRow);

            foreach (var serializedPath in serializedValidationState.SerializedValidationPaths)
                AddPathToList(serializedPath);

            Add(_pathSelectionColumn);
        }

        private void AddPathToList(string path)
        {
            var validationPath = new VisualElement();
            validationPath.AddToClassList("validation-path-row");

            var folderPathLabel = new Label(path);
            folderPathLabel.AddToClassList("path-input-field");
            _validationPaths.Add(folderPathLabel);

            var removeButton = new Button(() =>
            {
                _pathBoxScrollView.Remove(validationPath);
                _validationPaths.Remove(folderPathLabel);
            });
            removeButton.text = "X";
            removeButton.AddToClassList("validation-path-remove-button");

            validationPath.Add(folderPathLabel);
            validationPath.Add(removeButton);

            _pathBoxScrollView.Add(validationPath);
        }

        private void Browse()
        {
            string result = EditorUtility.OpenFolderPanel("Select a directory", "Assets", "");

            if (result == string.Empty)
                return;

            if (ValidateFolderPath(ref result) && !_validationPaths.Any(x => x.text == result))
                AddPathToList(result);
            else
                return;
        }
        
        private bool ValidateFolderPath(ref string resultPath)
        {
            var folderPath = resultPath;
            var pathWithinProject = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);

            // Selected path is within the project
            if (folderPath.StartsWith(pathWithinProject))
            {
                var localPath = folderPath.Substring(pathWithinProject.Length);

                if (localPath.StartsWith("Assets/") || localPath == "Assets")
                {
                    resultPath = localPath;
                    return true;
                }

                if (IsValidLocalPackage(localPath, out var adbPath))
                {
                    resultPath = adbPath;
                    return true;
                }

                DisplayMessage("Folder not found", "Selection must be within Assets folder or a local package.");
                return false;
            }
            
            bool validLocalPackage = IsValidLocalPackage(folderPath, out var relativePackagePath);

            bool isSymlinkedPath = false;
            string relativeSymlinkPath = string.Empty;
            
            if (ASToolsPreferences.Instance.EnableSymlinkSupport)
                isSymlinkedPath = SymlinkUtil.FindSymlinkFolderRelative(folderPath, out relativeSymlinkPath);
            
            // Selected path is not within the project, but could be a local package or symlinked folder
            if (!validLocalPackage && !isSymlinkedPath)
            {
                DisplayMessage("Folder not found", "Selection must be within Assets folder or a local package.");
                return false;
            }

            resultPath = validLocalPackage ? relativePackagePath : relativeSymlinkPath;
            return true;
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

        private void DisplayMessage(string title, string message)
        {
            if (EditorUtility.DisplayDialog(title, message, "Okay", "Cancel"))
                Browse();
        }
    }
}