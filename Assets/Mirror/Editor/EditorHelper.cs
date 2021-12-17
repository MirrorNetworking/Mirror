using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mirror
{
    public static class EditorHelper
    {
        public static string FindPath<T>()
        {
            string typeName = typeof(T).Name;

            string[] guidsFound = AssetDatabase.FindAssets($"t:Script {typeName}");
            if (guidsFound.Length >= 1 && !string.IsNullOrWhiteSpace(guidsFound[0]))
            {
                if (guidsFound.Length > 1)
                {
                    Debug.LogWarning($"Found more than one{typeName}");
                }

                string path = AssetDatabase.GUIDToAssetPath(guidsFound[0]);
                return Path.GetDirectoryName(path);
            }
            else
            {
                Debug.LogError($"Could not find path of {typeName}");
                return string.Empty;
            }
        }
    }
}
