using UnityEditor;
using UnityEngine;

namespace Mirror
{
    [InitializeOnLoad]
    public static class Janitor
    {
        private const string MirrorVersionKey = "Mirror_Package_Version_Key";
        static Janitor()
        {
            string currentVersion = GetCurrentPackageVersion();
            string storedVersion = EditorPrefs.GetString(MirrorVersionKey, "");
            if (currentVersion != storedVersion)
            {
                // Assumption: The package has been updated and thus cleaning should occur
                CleanUp();
                EditorPrefs.SetString(MirrorVersionKey, currentVersion);
            }
        }
        static string GetCurrentPackageVersion()
        {
            return "v86.7.2"; // What is a good way of getting the current Mirror package version?
        }
        static void CleanUp()
        {
            // pathsToRemove is only initialized if the package has been updated
            // to avoid unnecessary allocations
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
