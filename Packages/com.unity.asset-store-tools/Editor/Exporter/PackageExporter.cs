using AssetStoreTools.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;

namespace AssetStoreTools.Exporter
{
    internal abstract class PackageExporter
    {
        protected const string ProgressBarTitle = "Exporting Package";
        protected const string ProgressBarStepSavingAssets = "Saving Assets...";
        protected const string ProgressBarStepGatheringFiles = "Gathering files...";
        protected const string ProgressBarStepCompressingPackage = "Compressing package...";

        public static async Task<ExportResult> ExportPackage(ExporterSettings exportSettings)
        {
            if (!IsSettingsValid(exportSettings, out Exception e))
                return new ExportResult() { Success = false, Error = ASError.GetGenericError(e) };

            switch (exportSettings)
            {
                case LegacyExporterSettings legacySettings:
                    return await PackageExporterLegacy.ExportPackage(legacySettings);
                case DefaultExporterSettings defaultSettings:
                    return PackageExporterDefault.ExportPackage(defaultSettings);
                default:
                    return new ExportResult() { Success = false, Error = ASError.GetGenericError(new ArgumentException("Unrecognized ExportSettings type was provided")) };
            }
        }

        private static bool IsSettingsValid(ExporterSettings settings, out Exception e)
        {
            e = null;

            if (settings == null)
                e = new ArgumentException("Package Exporting failed: ExporterSettings cannot be null");
            else if (settings.ExportPaths == null || settings.ExportPaths.Length == 0)
                e = new ArgumentException("Package Exporting failed: received an invalid export paths array");
            else if (string.IsNullOrEmpty(settings.OutputFilename))
                e = new ArgumentException("Package Exporting failed: received an invalid output path");
            else if (settings.OutputFilename.EndsWith("/") || settings.OutputFilename.EndsWith("\\"))
                e = new ArgumentException("Package Exporting failed: output path must be a valid filename and not end with a directory separator character");

            return e == null;
        }

        protected string[] GetAssetPaths(string rootPath)
        {
            // To-do: slight optimization is possible in the future by having a list of excluded folders/file extensions
            List<string> paths = new List<string>();

            // Add files within given directory
            var filePaths = Directory.GetFiles(rootPath).Select(p => p.Replace('\\', '/')).ToArray();
            paths.AddRange(filePaths);

            // Add directories within given directory
            var directoryPaths = Directory.GetDirectories(rootPath).Select(p => p.Replace('\\', '/')).ToArray();
            foreach (var nestedDirectory in directoryPaths)
                paths.AddRange(GetAssetPaths(nestedDirectory));

            // Add the given directory itself if it is not empty
            if (filePaths.Length > 0 || directoryPaths.Length > 0)
                paths.Add(rootPath);

            return paths.ToArray();
        }

        protected string GetAssetGuid(string assetPath, bool hiddenSearch)
        {
            // Skip meta files as they do not have guids
            if (assetPath.EndsWith(".meta"))
                return string.Empty;

            // Skip hidden assets. They normally do not have meta files, but
            // have been observed to retain them in the past due to a Unity bug
            if (assetPath.EndsWith("~"))
                return string.Empty;

            // Attempt retrieving guid from the Asset Database first
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (guid != string.Empty)
                return guid;

            // Files in hidden folders (e.g. Samples~) are not part of the Asset Database,
            // therefore GUIDs need to be scraped from the .meta file.
            // Note: only do this for custom exporter since the native exporter
            // will not be able to retrieve the asset path from a hidden folder
            if (hiddenSearch)
            {
                // To-do: handle hidden folders without meta files
                var metaPath = $"{assetPath}.meta";

                if (!File.Exists(metaPath))
                    return string.Empty;

                using (StreamReader reader = new StreamReader(metaPath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != string.Empty)
                    {
                        if (!line.StartsWith("guid:"))
                            continue;
                        var metaGuid = line.Substring("guid:".Length).Trim();
                        return metaGuid;
                    }
                }
            }

            return string.Empty;
        }

        protected virtual void PostExportCleanup()
        {
            EditorUtility.ClearProgressBar();
        }
    }
}