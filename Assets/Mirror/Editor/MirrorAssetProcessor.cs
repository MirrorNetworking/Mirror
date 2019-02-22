/*
MIT License

Pull request submitted by Digital Ruby, LLC (https://www.digitalruby.com)

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using UnityEngine;
using UnityEditor;

using System.Collections.Generic;
using System.IO;

namespace DigitalRuby.WeatherMaker
{
    /// <summary>
    /// Pre-process define utility
    /// </summary>
    public class MirrorAssetPostprocessor : AssetPostprocessor
    {
        // add list of asset files to key off of along with pre-processor definition
        // asset name / pre processor value key / value pair
        internal static readonly KeyValuePair<string, string>[] preProcessors = new KeyValuePair<string, string>[]
        {
            new KeyValuePair<string, string>("/Mirror/Runtime/NetworkIdentity.cs", "MIRROR_NET"),
        };

        /// <summary>
        /// Update pre-processor
        /// </summary>
        /// <param name="hasAsset">Whether the asset exists, if it does pre-processor is added, else it is removed if it exists</param>
        /// <param name="preProcessor">The pre-processor to add or remove</param>
        internal static void UpdatePreProcessor(bool hasAsset, string preProcessor)
        {
            string currBuildSettings = UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup(UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup);
            string currBuildSettingsCompare = currBuildSettings;
            if (hasAsset)
            {
                if (!currBuildSettings.Contains(preProcessor))
                {
                    currBuildSettings += ";" + preProcessor;
                }
            }
            else
            {
                currBuildSettings = System.Text.RegularExpressions.Regex.Replace(currBuildSettings, ";?" + preProcessor + ";?", string.Empty);
            }
            if (currBuildSettings != currBuildSettingsCompare)
            {
                Debug.LogWarning("Updating preprocessor to " + currBuildSettings);
                UnityEditor.PlayerSettings.SetScriptingDefineSymbolsForGroup(UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup, currBuildSettings);
            }
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string assetName in importedAssets)
            {
                foreach (var kv in preProcessors)
                {
                    if (assetName.IndexOf(kv.Key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        UpdatePreProcessor(true, kv.Value);
                    }
                }
            }
            foreach (string assetName in deletedAssets)
            {
                foreach (var kv in preProcessors)
                {
                    if (assetName.IndexOf(kv.Key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        UpdatePreProcessor(false, kv.Value);
                    }
                }
            }
        }
    }

    public class MirrorAssetModificationProcessor : UnityEditor.AssetModificationProcessor
    {
        private static void OnWillCreateAsset(string assetName)
        {
            foreach (var kv in MirrorAssetPostprocessor.preProcessors)
            {
                if (assetName.IndexOf(kv.Key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    MirrorAssetPostprocessor.UpdatePreProcessor(true, kv.Value);
                }
            }
        }

        private static AssetDeleteResult OnWillDeleteAsset(string assetName, RemoveAssetOptions options)
        {
            // this only gets called for the directory on deletion, not each file in the directory, so we have to manually scan the dir
            // before it is deleted
            if (Directory.Exists(assetName))
            {
                foreach (string file in Directory.GetFiles(assetName, "*", SearchOption.AllDirectories))
                {
                    string normFile = file.Replace("\\", "/");
                    foreach (var kv in MirrorAssetPostprocessor.preProcessors)
                    {
                        if (normFile.IndexOf(kv.Key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            MirrorAssetPostprocessor.UpdatePreProcessor(false, kv.Value);
                        }
                    }
                }
            }
            else
            {
                foreach (var kv in MirrorAssetPostprocessor.preProcessors)
                {
                    if (assetName.IndexOf(kv.Key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        MirrorAssetPostprocessor.UpdatePreProcessor(false, kv.Value);
                    }
                }
            }
            return AssetDeleteResult.DidNotDelete;
        }
    }
}
