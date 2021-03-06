using AssetStoreTools.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace AssetStoreTools.Validator.TestMethods.Utility
{
    internal enum AssetType
    {
        All,
        Documentation,
        Executable,
        JPG,
        JavaScript,
        LossyAudio,
        Material,
        Mixamo,
        Model,
        MonoScript,
        NonLossyAudio,
        PrecompiledAssembly,
        Prefab,
        Scene,
        Shader,
        SpeedTree,
        Texture,
        Video
    }

    internal static class AssetUtility
    {
        public static IEnumerable<string> GetAssetPathsFromAssets(string[] searchPaths, AssetType type)
        {
            string filter = string.Empty;
            string[] extensions = null;

            switch (type)
            {
                // General Types
                case AssetType.All:
                    filter = "";
                    break;
                case AssetType.Prefab:
                    filter = "t:prefab";
                    break;
                case AssetType.Material:
                    filter = "t:material";
                    break;
                case AssetType.Model:
                    filter = "t:model";
                    break;
                case AssetType.Scene:
                    filter = "t:scene";
                    break;
                case AssetType.Texture:
                    filter = "t:texture";
                    break;
                case AssetType.Video:
                    filter = "t:VideoClip";
                    break;
                // Specific Types
                case AssetType.LossyAudio:
                    filter = "t:AudioClip";
                    extensions = new[] { ".mp3", ".ogg" };
                    break;
                case AssetType.NonLossyAudio:
                    filter = "t:AudioClip";
                    extensions = new[] { ".wav", ".aif", ".aiff" };
                    break;
                case AssetType.JavaScript:
                    filter = "t:TextAsset";
                    extensions = new[] { ".js" };
                    break;
                case AssetType.Mixamo:
                    filter = "t:model";
                    extensions = new[] { ".fbx" };
                    break;
                case AssetType.JPG:
                    filter = "t:texture";
                    extensions = new[] { ".jpg", "jpeg" };
                    break;
                case AssetType.Executable:
                    filter = string.Empty;
                    extensions = new[] { ".exe", ".bat", ".msi", ".apk" };
                    break;
                case AssetType.Documentation:
                    filter = string.Empty;
                    extensions = new[] { ".txt", ".pdf", ".html", ".rtf", ".md" };
                    break;
                case AssetType.SpeedTree:
                    filter = string.Empty;
                    extensions = new[] { ".spm", ".srt", ".stm", ".scs", ".sfc", ".sme", ".st" };
                    break;
                case AssetType.Shader:
                    filter = string.Empty;
                    extensions = new[] { ".shader", ".shadergraph", ".raytrace", ".compute" };
                    break;
                case AssetType.MonoScript:
                    filter = "t:script";
                    extensions = new[] { ".cs" };
                    break;
                case AssetType.PrecompiledAssembly:
                    var assemblyPaths = GetPrecompiledAssemblies(searchPaths);
                    return assemblyPaths;
                default:
                    return Array.Empty<string>();
            }

            var guids = AssetDatabase.FindAssets(filter, searchPaths);
            var paths = guids.Select(AssetDatabase.GUIDToAssetPath);

            if (extensions != null)
                paths = paths.Where(x => extensions.Any(x.ToLower().EndsWith));

            if (type == AssetType.Mixamo)
                paths = paths.Where(IsMixamoFbx);

            paths = paths.Distinct();
            return paths;
        }

        public static IEnumerable<T> GetObjectsFromAssets<T>(string[] searchPaths, AssetType type) where T : UnityObject
        {
            var paths = GetAssetPathsFromAssets(searchPaths, type);
            var objects = paths.Select(AssetDatabase.LoadAssetAtPath<T>).Where(x => x != null);
            return objects;
        }

        public static IEnumerable<UnityObject> GetObjectsFromAssets(string[] searchPaths, AssetType type)
        {
            return GetObjectsFromAssets<UnityObject>(searchPaths, type);
        }

        private static IEnumerable<string> GetPrecompiledAssemblies(string[] searchPaths)
        {
            // Note - for packages, Compilation Pipeline returns full paths, as they appear on disk, not Asset Database
            var allDllPaths = CompilationPipeline.GetPrecompiledAssemblyPaths(CompilationPipeline.PrecompiledAssemblySources.UserAssembly);
            var rootProjectPath = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
            var packages = PackageUtility.GetAllLocalPackages();

            var result = new List<string>();
            foreach (var dllPath in allDllPaths)
            {
                var absoluteDllPath = Path.GetFullPath(dllPath).Replace("\\", "/");
                foreach (var validationPath in searchPaths)
                {
                    var absoluteValidationPath = Path.GetFullPath(validationPath).Replace("\\", "/");
                    if (absoluteDllPath.StartsWith(absoluteValidationPath))
                    {
                        int pathSeparatorLength = 1;
                        if (absoluteDllPath.StartsWith(Application.dataPath))
                        {
                            var adbPath = $"Assets/{absoluteDllPath.Remove(0, Application.dataPath.Length + pathSeparatorLength)}";
                            result.Add(adbPath);
                        }
                        else
                        {
                            // For non-Asset folder paths (i.e. local and embedded packages), convert disk path to ADB path
                            var package = packages.FirstOrDefault(x => dllPath.StartsWith(x.resolvedPath.Replace('\\', '/')));

                            if (package == null)
                                continue;

                            var dllPathInPackage = absoluteDllPath.Remove(0, Path.GetFullPath(package.resolvedPath).Length + pathSeparatorLength);
                            var adbPath = $"Packages/{package.name}/{dllPathInPackage}";

                            result.Add(adbPath);
                        }
                    }
                }
            }

            return result;
        }

        private static bool IsMixamoFbx(string fbxPath)
        {
            // Location of Mixamo Header, this is located in every mixamo fbx file exported
            //const int mixamoHeader = 0x4c0 + 2; // < this is the original location from A$ Tools, unsure if Mixamo file headers were changed since then
            const int mixamoHeader = 1622;
            // Length of Mixamo header
            const int length = 0xa;

            var fs = new FileStream(fbxPath, FileMode.Open);
            // Check if length is further than
            if (fs.Length < mixamoHeader)
                return false;

            byte[] buffer = new byte[length];
            using (BinaryReader reader = new BinaryReader(fs))
            {
                reader.BaseStream.Seek(mixamoHeader, SeekOrigin.Begin);
                reader.Read(buffer, 0, length);
            }

            string result = System.Text.Encoding.ASCII.GetString(buffer);
            return result.Contains("Mixamo");
        }

        public static string ObjectToAssetPath(UnityObject obj)
        {
            return AssetDatabase.GetAssetPath(obj);
        }

        public static T AssetPathToObject<T>(string assetPath) where T : UnityObject
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetPath);
        }

        public static UnityObject AssetPathToObject(string assetPath)
        {
            return AssetPathToObject<UnityObject>(assetPath);
        }

        public static AssetImporter GetAssetImporter(string assetPath)
        {
            return AssetImporter.GetAtPath(assetPath);
        }

        public static AssetImporter GetAssetImporter(UnityObject asset)
        {
            return GetAssetImporter(ObjectToAssetPath(asset));
        }
    }
}