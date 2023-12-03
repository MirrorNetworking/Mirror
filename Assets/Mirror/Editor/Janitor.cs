using UnityEditor;
using UnityEngine;

namespace Mirror
{
    [InitializeOnLoad]
    // This editor script cleans up deleted files and folders from previous Mirror versions
    // because the Unity Asset Store doesn't delete them (instead orphaning them upon package upgrade)
    public static class Janitor
    {
        private const string JanitorVersionKey = "Mirror_Janitor_Version";
        static Janitor()
        {
            string currentVersion = GetJanitorVersion();
            string storedVersion = EditorPrefs.GetString(JanitorVersionKey, "");
            if (currentVersion != storedVersion)
            {
                CleanUp();
                EditorPrefs.SetString(JanitorVersionKey, currentVersion);
            }
        }
        static string GetJanitorVersion()
        {
            return "1"; // increment this when adding new cleanups
        }
        static void CleanUp()
        {
            // pathsToRemove is only initialized if the package has been updated
            // to avoid unnecessary allocations
            // note: GetJanitorVersion value should change when adding new paths
            string[] pathsToRemove = {
                "Assets/Mirror/Core/Empty",
                "Assets/Mirror/Transports/Telepathy/Telepathy/Empty",
                "Assets/Mirror/Editor/Empty",
                "Assets/Mirror/Editor/Weaver/Empty",
                "Assets/Mirror/Transports/KCP/kcp2k/empty",
                "Assets/Mirror/Hosting/Edgegap/Editor/EdgegapToolScript.cs",
                "Assets/Mirror/Hosting/Edgegap/Editor/EdgegapScriptEditor.cs",
            };

            foreach (string path in pathsToRemove)
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                {
                    AssetDatabase.DeleteAsset(path);
                    Debug.Log($"Mirror | mirror-networking.com | Removed outdated: {path}");
                }
            }

            AssetDatabase.Refresh();
        }
    }
}
