using AssetStoreTools.Exporter;
using AssetStoreTools.Utility;
using AssetStoreTools.Utility.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine.UIElements;

namespace AssetStoreTools.Uploader.UIElements
{
    internal abstract class UploadWorkflowView : VisualElement
    {
        protected TextField PathSelectionField;

        // Upload data
        protected List<string> ExtraExportPaths = new List<string>();

        protected string MainExportPath = String.Empty;
        protected string LocalPackageGuid;
        protected string LocalPackagePath;
        protected string LocalProjectPath;

        protected string Category;
        protected ValidationElement ValidationElement;

        protected readonly Action SerializeSelection;

        public abstract string Name { get; }
        public abstract string DisplayName { get; }

        protected UploadWorkflowView(Action serializeSelection)
        {
            SerializeSelection = serializeSelection;
            style.display = DisplayStyle.None;
        }

        public string[] GetAllExportPaths()
        {
            var allPaths = new List<string>(ExtraExportPaths);
            if (!string.IsNullOrEmpty(MainExportPath))
                allPaths.Insert(0, MainExportPath);
            return allPaths.ToArray();
        }

        public string GetLocalPackageGuid()
        {
            return LocalPackageGuid;
        }

        public string GetLocalPackagePath()
        {
            return LocalPackagePath;
        }

        public string GetLocalProjectPath()
        {
            return LocalProjectPath;
        }

        protected abstract void SetupWorkflow();

        public virtual JsonValue SerializeWorkflow()
        {
            var workflowDict = JsonValue.NewDict();

            var mainExportPathDict = JsonValue.NewDict();
            mainExportPathDict["path"] = MainExportPath;

            if (MainExportPath != null && !MainExportPath.StartsWith("Assets/") && !MainExportPath.StartsWith("Packages/"))
                mainExportPathDict["guid"] = new JsonValue("");
            else
                mainExportPathDict["guid"] = AssetDatabase.AssetPathToGUID(MainExportPath);

            workflowDict["mainPath"] = mainExportPathDict;

            var extraExportPathsList = new List<JsonValue>();
            foreach (var path in ExtraExportPaths)
            {
                var pathDict = JsonValue.NewDict();
                pathDict["path"] = path;
                pathDict["guid"] = AssetDatabase.AssetPathToGUID(path);
                extraExportPathsList.Add(pathDict);
            }
            workflowDict["extraPaths"] = extraExportPathsList;

            return workflowDict;
        }

        protected bool DeserializeMainExportPath(JsonValue json, out string mainExportPath)
        {
            mainExportPath = string.Empty;
            try
            {
                var mainPathDict = json["mainPath"];

                if (!mainPathDict.ContainsKey("path") || !mainPathDict["path"].IsString())
                    return false;

                mainExportPath = DeserializePath(mainPathDict);
                return true;
            }
            catch
            {
                return false;
            }
        }

        protected void DeserializeExtraExportPaths(JsonValue json, out List<string> extraExportPaths)
        {
            extraExportPaths = new List<string>();
            try
            {
                var extraPathsList = json["extraPaths"].AsList();
                extraExportPaths.AddRange(extraPathsList.Select(DeserializePath));
            }
            catch
            {
                ASDebug.LogWarning($"Deserializing extra export paths for {Name} failed");
                extraExportPaths.Clear();
            }
        }
        
        protected void DeserializeDependencies(JsonValue json, out List<string> dependencies)
        {
            dependencies = new List<string>();
            try
            {
                var packageJsonList = json["dependenciesNames"].AsList();
                dependencies.AddRange(packageJsonList.Select(package => package.AsString()));
            }
            catch
            {
                ASDebug.LogWarning($"Deserializing dependencies for {Name} failed");
                dependencies.Clear();
            }
        }

        protected void DeserializeDependenciesToggle(JsonValue json, out bool dependenciesBool)
        {
            bool includeDependencies;
            try
            {
                includeDependencies = json["dependencies"].AsBool();
            }
            catch
            {
                ASDebug.LogWarning($"Deserializing dependencies toggle for {Name} failed");
                includeDependencies = false;
            }

            dependenciesBool = includeDependencies;
        }
        

        private string DeserializePath(JsonValue pathDict)
        {
            // First pass - retrieve from GUID
            var exportPath = AssetDatabase.GUIDToAssetPath(pathDict["guid"].AsString());
            // Second pass - retrieve directly
            if (string.IsNullOrEmpty(exportPath))
                exportPath = pathDict["path"].AsString();
            return exportPath;
        }

        public bool GetValidationSummary(out string validationSummary)
        {
            return ValidationElement.GetValidationSummary(out validationSummary);
        }

        public abstract void LoadSerializedWorkflow(JsonValue json, string lastUploadedPath, string lastUploadedGuid);

        public abstract void LoadSerializedWorkflowFallback(string lastUploadedPath, string lastUploadedGuid);

        protected abstract void BrowsePath();

        public abstract Task<ExportResult> ExportPackage(string outputPath, bool isCompleteProject);
    }
}