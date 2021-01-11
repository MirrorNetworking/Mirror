using UnityEditor;
using UnityEngine;

namespace Mirror.EditorScripts
{
    public static class ScriptableObjectUtility
    {
        const string DefaultMirrorFolder = "Assets/Mirror/";

        [System.Obsolete("Use CreateAsset<T>(string defaultName, string defaultFolder) instead")]
        public static T CreateAsset<T>(string defaultName) where T : ScriptableObject
            => CreateAsset<T>(defaultName, DefaultMirrorFolder);

        /// <summary>
        ///	This makes it easy to create, name and place unique new ScriptableObject asset files.
        /// </summary>
        public static T CreateAsset<T>(string defaultName, string defaultFolder) where T : ScriptableObject
        {
            string path = SavePanel(defaultName, defaultFolder);
            // user click cancel
            if (string.IsNullOrEmpty(path)) { return null; }

            T asset = ScriptableObject.CreateInstance<T>();

            SaveAsset(path, asset);

            return asset;
        }

        static string SavePanel(string name, string defaultFolder)
        {
            string path = EditorUtility.SaveFilePanel(
                           "Save ScriptableObject",
                           defaultFolder,
                           name + ".asset",
                           "asset");

            // user click cancel, return early
            if (string.IsNullOrEmpty(path)) { return path; }

            // Unity only wants path from Assets
            if (path.StartsWith(Application.dataPath))
            {
                path = "Assets" + path.Substring(Application.dataPath.Length);
            }

            return path;
        }

        static void SaveAsset(string path, ScriptableObject asset)
        {
            string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(path);

            AssetDatabase.CreateAsset(asset, assetPathAndName);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
