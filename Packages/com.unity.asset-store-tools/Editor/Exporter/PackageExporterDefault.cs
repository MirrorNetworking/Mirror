using AssetStoreTools.Uploader.Utility;
using AssetStoreTools.Utility;
using AssetStoreTools.Utility.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AssetStoreTools.Exporter
{
    internal class PackageExporterDefault : PackageExporter
    {
        private const string TemporaryExportPathName = "CustomExport";
        private const string ManifestJsonPath = "Packages/manifest.json";

        private DefaultExporterSettings _exportSettings;

        private PackageExporterDefault(DefaultExporterSettings exportSettings)
        {
            _exportSettings = exportSettings;
        }

        public static ExportResult ExportPackage(DefaultExporterSettings exportSettings)
        {
            var exporter = new PackageExporterDefault(exportSettings);
            return exporter.ExportPackage();
        }

        private ExportResult ExportPackage()
        {
            ASDebug.Log("Using custom package exporter");

            // Save assets before exporting
            EditorUtility.DisplayProgressBar(ProgressBarTitle, ProgressBarStepSavingAssets, 0.1f);
            AssetDatabase.SaveAssets();

            try
            {
                // Create a temporary export path
                var temporaryExportPath = GetTemporaryExportPath();
                if (!Directory.Exists(temporaryExportPath))
                    Directory.CreateDirectory(temporaryExportPath);

                // Construct an unzipped package structure
                CreateTempPackageStructure(temporaryExportPath);

                // Build a .unitypackage file from the temporary folder
                CreateUnityPackage(temporaryExportPath, _exportSettings.OutputFilename);

                EditorUtility.RevealInFinder(_exportSettings.OutputFilename);

                ASDebug.Log($"Package file has been created at {_exportSettings.OutputFilename}");
                return new ExportResult() { Success = true, ExportedPath = _exportSettings.OutputFilename };
            }
            catch (Exception e)
            {
                return new ExportResult() { Success = false, Error = ASError.GetGenericError(e) };
            }
            finally
            {
                PostExportCleanup();
            }
        }

        private string GetTemporaryExportPath()
        {
            return $"{AssetStoreCache.TempCachePath}/{TemporaryExportPathName}";
        }

        private void CreateTempPackageStructure(string tempOutputPath)
        {
            EditorUtility.DisplayProgressBar(ProgressBarTitle, ProgressBarStepGatheringFiles, 0.4f);
            var pathGuidPairs = GetPathGuidPairs(_exportSettings.ExportPaths);

            // Caching asset previews takes time, so we'll start doing it as we
            // iterate through assets and only retrieve them after generating the rest
            // of the package structure
            AssetPreview.SetPreviewTextureCacheSize(pathGuidPairs.Count + 100);
            var pathObjectPairs = new Dictionary<string, UnityEngine.Object>();

            foreach (var pair in pathGuidPairs)
            {
                var originalAssetPath = pair.Key;
                var outputAssetPath = $"{tempOutputPath}/{pair.Value}";

                if (Directory.Exists(outputAssetPath))
                {
                    var path1 = File.ReadAllText($"{outputAssetPath}/pathname");
                    var path2 = originalAssetPath;
                    throw new InvalidOperationException($"Multiple assets with guid {pair.Value} have been detected " +
                        $"when exporting the package. Please resolve the guid conflicts and try again:\n{path1}\n{path2}");
                }

                Directory.CreateDirectory(outputAssetPath);

                // Every exported asset has a pathname file
                using (StreamWriter writer = new StreamWriter($"{outputAssetPath}/pathname"))
                    writer.Write(originalAssetPath);

                // Only files (not folders) have an asset file
                if (File.Exists(originalAssetPath))
                    File.Copy(originalAssetPath, $"{outputAssetPath}/asset");

                // Most files and folders have an asset.meta file (but ProjectSettings folder assets do not)
                if (File.Exists($"{originalAssetPath}.meta"))
                    File.Copy($"{originalAssetPath}.meta", $"{outputAssetPath}/asset.meta");

                // To-do: handle previews in hidden folders as they are not part of the AssetDatabase
                var previewObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(originalAssetPath);
                if (previewObject == null)
                    continue;
                // Start caching the asset preview
                AssetPreview.GetAssetPreview(previewObject);
                pathObjectPairs.Add(outputAssetPath, previewObject);
            }

            WritePreviewTextures(pathObjectPairs);

            if (_exportSettings.Dependencies == null || _exportSettings.Dependencies.Length == 0)
                return;

            var manifestJson = GetPackageManifestJson();
            var allDependenciesDict = manifestJson["dependencies"].AsDict();

            var allLocalPackages = PackageUtility.GetAllLocalPackages();
            List<string> allPackagesList = new List<string>(allDependenciesDict.Keys);

            foreach (var package in allPackagesList)
            {
                if (!_exportSettings.Dependencies.Any(x => x == package))
                {
                    allDependenciesDict.Remove(package);
                    continue;
                }

                if (!allLocalPackages.Select(x => x.name).Contains(package))
                    continue;

                allDependenciesDict.Remove(package);
                UnityEngine.Debug.LogWarning($"Found an unsupported Package Manager dependency: {package}.\n" +
                                             "This dependency is not supported in the project's manifest.json and will be skipped.");
            }

            if (allDependenciesDict.Count == 0)
                return;

            var tempManifestDirectoryPath = $"{tempOutputPath}/packagemanagermanifest";
            Directory.CreateDirectory(tempManifestDirectoryPath);
            var tempManifestFilePath = $"{tempManifestDirectoryPath}/asset";

            File.WriteAllText(tempManifestFilePath, manifestJson.ToString());
        }

        private Dictionary<string, string> GetPathGuidPairs(string[] exportPaths)
        {
            var pathGuidPairs = new Dictionary<string, string>();

            foreach (var exportPath in exportPaths)
            {
                var assetPaths = GetAssetPaths(exportPath);

                foreach (var assetPath in assetPaths)
                {
                    var guid = GetAssetGuid(assetPath, true);
                    if (string.IsNullOrEmpty(guid))
                        continue;

                    pathGuidPairs.Add(assetPath, guid);
                }
            }

            return pathGuidPairs;
        }

        private void WritePreviewTextures(Dictionary<string, UnityEngine.Object> pathObjectPairs)
        {
            foreach (var kvp in pathObjectPairs)
            {
                var obj = kvp.Value;
                var queuePreview = false;

                switch (obj)
                {
                    case Material _:
                    case TerrainLayer _:
                    case AudioClip _:
                    case Mesh _:
                    case Texture _:
                    case UnityEngine.Tilemaps.Tile _:
                    case GameObject _:
                        queuePreview = true;
                        break;
                }

                if (!queuePreview)
                    continue;

                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out var guid, out long _);
                var preview = GetAssetPreviewFromGuid(guid);

                if (!preview)
                    continue;

                var thumbnailWidth = Mathf.Min(preview.width, 128);
                var thumbnailHeight = Mathf.Min(preview.height, 128);
                var rt = RenderTexture.GetTemporary(thumbnailWidth, thumbnailHeight, 0, RenderTextureFormat.Default, RenderTextureReadWrite.sRGB);

                var copy = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);

                RenderTexture.active = rt;
                GL.Clear(true, true, new Color(0, 0, 0, 0));
                Graphics.Blit(preview, rt);
                copy.ReadPixels(new Rect(0, 0, copy.width, copy.height), 0, 0, false);
                copy.Apply();
                RenderTexture.active = null;

                var bytes = copy.EncodeToPNG();
                if (bytes != null && bytes.Length > 0)
                {
                    File.WriteAllBytes(kvp.Key + "/preview.png", bytes);
                }

                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private Texture2D GetAssetPreviewFromGuid(string guid)
        {
            var method = typeof(AssetPreview).GetMethod("GetAssetPreviewFromGUID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(string) }, null);
            var args = new object[] { guid };

            return method?.Invoke(null, args) as Texture2D;
        }

        private void CreateUnityPackage(string pathToArchive, string outputPath)
        {
            if (Directory.GetDirectories(pathToArchive).Length == 0)
                throw new InvalidOperationException("Unable to export package. The specified path is empty");

            EditorUtility.DisplayProgressBar(ProgressBarTitle, ProgressBarStepCompressingPackage, 0.5f);

            // Archiving process working path will be set to the
            // temporary package path so adjust the output path accordingly
            if (!Path.IsPathRooted(outputPath))
                outputPath = $"{Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length)}/{outputPath}";

#if UNITY_EDITOR_WIN
            CreateUnityPackageUniversal(pathToArchive, outputPath);
#elif UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
        CreateUnityPackageOsxLinux(pathToArchive, outputPath);
#endif
        }

        private void CreateUnityPackageUniversal(string pathToArchive, string outputPath)
        {
            var _7zPath = EditorApplication.applicationContentsPath;
#if UNITY_EDITOR_WIN
            _7zPath = Path.Combine(_7zPath, "Tools", "7z.exe");
#elif UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            _7zPath = Path.Combine(_7zPath, "Tools", "7za");
#endif
            if (!File.Exists(_7zPath))
                throw new FileNotFoundException("Archiving utility was not found in your Unity installation directory");

            var argumentsTar = $"a -r -ttar -y -bd archtemp.tar .";
            var result = StartProcess(_7zPath, argumentsTar, pathToArchive);
            if (result != 0)
                throw new Exception("Failed to compress the package");

            // Create a GZIP archive
            var argumentsGzip = $"a -tgzip -bd -y \"{outputPath}\" archtemp.tar";
            result = StartProcess(_7zPath, argumentsGzip, pathToArchive);
            if (result != 0)
                throw new Exception("Failed to compress the package");
        }

        private void CreateUnityPackageOsxLinux(string pathToArchive, string outputPath)
        {
            var tarPath = "/usr/bin/tar";

            if (!File.Exists(tarPath))
            {
                // Fallback to the universal export method
                ASDebug.LogWarning("'/usr/bin/tar' executable not found. Falling back to 7za");
                CreateUnityPackageUniversal(pathToArchive, outputPath);
                return;
            }

            // Create a TAR archive
            var arguments = $"-czpf \"{outputPath}\" .";
            var result = StartProcess(tarPath, arguments, pathToArchive);
            if (result != 0)
                throw new Exception("Failed to compress the package");
        }

        private int StartProcess(string processPath, string arguments, string workingDirectory)
        {
            var info = new ProcessStartInfo()
            {
                FileName = processPath,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using (Process process = Process.Start(info))
            {
                process.WaitForExit();
                return process.ExitCode;
            }
        }

        private JsonValue GetPackageManifestJson()
        {
            string manifestJsonString = File.ReadAllText(ManifestJsonPath);
            JSONParser parser = new JSONParser(manifestJsonString);
            var manifestJson = parser.Parse();

            return manifestJson;
        }

        protected override void PostExportCleanup()
        {
            base.PostExportCleanup();

            var tempExportPath = GetTemporaryExportPath();
            if (Directory.Exists(tempExportPath))
                Directory.Delete(tempExportPath, true);
        }
    }
}