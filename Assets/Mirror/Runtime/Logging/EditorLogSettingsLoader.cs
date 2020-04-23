using UnityEditor;
using UnityEngine;

namespace Mirror.Logging
{
#if UNITY_EDITOR
    public static class EditorLogSettingsLoader
    {
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
            Debug.Assert(assetGuids.Length < 2, "Found more than one LogSettings, Delete extra settinsg. Using first asset found :" + firstGuid);

            string path = AssetDatabase.GUIDToAssetPath(firstGuid);
            cache = AssetDatabase.LoadAssetAtPath<LogSettings>(path);
            return cache;
        }
    }
#endif
}
