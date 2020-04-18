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

            string[] assets = AssetDatabase.FindAssets("t:" + nameof(LogSettings));
            if (assets.Length == 0)
                return null;

            string firstPath = assets[0];
            Debug.Assert(assets.Length < 2, "Found more than one LogSettings, Delete extra settinsg. Using first asset found :" + firstPath);

            cache = AssetDatabase.LoadAssetAtPath<LogSettings>(firstPath);
            return cache;
        }

    }
#endif
}
