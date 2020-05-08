using UnityEditor;
using UnityEngine;

namespace Mirror.Logging
{
#if UNITY_EDITOR
    public static class EditorLogSettingsLoader
    {
        [InitializeOnLoadMethod]
        static void Init()
        {
            // load settings first time LogFactory is used in the editor
            LoadLogSettingsIntoDictionary();
        }

        public static void LoadLogSettingsIntoDictionary()
        {
            LogSettings settings = FindLogSettings();
            if (settings != null)
            {
                settings.LoadIntoDictionary(LogFactory.loggers);
            }
        }

        static LogSettings cache;
        public static LogSettings FindLogSettings()
        {
            if (cache != null)
                return cache;

            string[] assetGuids = AssetDatabase.FindAssets("t:" + nameof(LogSettings));
            if (assetGuids.Length == 0)
                return null;

            string firstGuid = assetGuids[0];

            string path = AssetDatabase.GUIDToAssetPath(firstGuid);
            cache = AssetDatabase.LoadAssetAtPath<LogSettings>(path);

            if (assetGuids.Length > 2)
            {
                Debug.LogWarning("Found more than one LogSettings, Delete extra settings. Using first asset found: " + path);
            }
            Debug.Assert(cache != null, "Failed to load asset at: " + path);

            return cache;
        }
    }
#endif
}
