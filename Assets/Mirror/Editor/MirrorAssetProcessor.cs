using UnityEngine;
using UnityEditor;

using System.Collections.Generic;
using System.IO;

namespace Mirror
{
    /// <summary>
    /// Pre-process define utility
    /// </summary>
    public class MirrorAssetPostprocessor : AssetPostprocessor
    {
        private const string assetToCheck = "/Mirror/Runtime/NetworkIdentity.cs";
        private const string preProcessor = "MIRROR_NET";

        /// <summary>
        /// Add the pre-processor
        /// </summary>
        internal static void AddPreProcessor()
        {
            string currBuildSettings = UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup(UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup);
            if (!currBuildSettings.Contains(preProcessor))
            {
                Debug.LogWarning("Updating preprocessor to " + currBuildSettings);
                currBuildSettings += ";" + preProcessor;
                UnityEditor.PlayerSettings.SetScriptingDefineSymbolsForGroup(UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup, currBuildSettings);
            }
        }

        /// <summary>
        /// Remove the pre-processor
        /// </summary>
        internal static void RemovePreProcessor()
        {
            string currBuildSettings = UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup(UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup);
            int origCurrBuildSettingsLength = currBuildSettings.Length;
            currBuildSettings = System.Text.RegularExpressions.Regex.Replace(currBuildSettings, ";?" + preProcessor + ";?", string.Empty);
            if (currBuildSettings.Length != origCurrBuildSettingsLength)
            {
                Debug.LogWarning("Updating preprocessor to " + currBuildSettings);
                UnityEditor.PlayerSettings.SetScriptingDefineSymbolsForGroup(UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup, currBuildSettings);
            }
        }

        internal static void ProcessImportedAssets(params string[] importedAssets)
        {
            if (importedAssets == null)
            {
                return;
            }

            foreach (string assetName in importedAssets)
            {
                if (assetName.IndexOf(assetToCheck, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AddPreProcessor();
                    return;
                }
            }
        }

        internal static void ProcessDeletedAssets(params string[] deletedAssets)
        {
            if (deletedAssets == null)
            {
                return;
            }

            foreach (string assetName in deletedAssets)
            {
                // Unity does not send deletion events for contained files if you delete a folder
                if (Directory.Exists(assetName))
                {
                    foreach (string file in Directory.GetFiles(assetName, "*", SearchOption.AllDirectories))
                    {
                        string normFile = file.Replace("\\", "/");
                        if (normFile.IndexOf(assetToCheck, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            RemovePreProcessor();
                            return;
                        }
                    }
                }
                else
                {
                    if (assetName.IndexOf(assetToCheck, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        RemovePreProcessor();
                        return;
                    }
                }
            }
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            ProcessImportedAssets(importedAssets);
            ProcessDeletedAssets(deletedAssets);
        }
    }

    public class MirrorAssetModificationProcessor : UnityEditor.AssetModificationProcessor
    {
        private static void OnWillCreateAsset(string assetName)
        {
            MirrorAssetPostprocessor.ProcessImportedAssets(assetName);
        }

        private static AssetDeleteResult OnWillDeleteAsset(string assetName, RemoveAssetOptions options)
        {
            MirrorAssetPostprocessor.ProcessDeletedAssets(assetName);
            return AssetDeleteResult.DidNotDelete;
        }
    }
}
