using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AssetStoreTools.Utility
{

    internal class ASToolsPreferences
    {
        private static ASToolsPreferences s_instance;
        public static ASToolsPreferences Instance => s_instance ?? (s_instance = new ASToolsPreferences());

        public static event Action OnSettingsChange;
        
        private ASToolsPreferences()
        {
            Load();
        }

        private void Load()
        {
            LegacyVersionCheck = PlayerPrefs.GetInt("AST_LegacyVersionCheck", 1) == 1;
            UploadVersionCheck = PlayerPrefs.GetInt("AST_UploadVersionCheck", 1) == 1;
            EnableSymlinkSupport = PlayerPrefs.GetInt("AST_EnableSymlinkSupport", 0) == 1;
            UseLegacyExporting = PlayerPrefs.GetInt("AST_UseLegacyExporting", 0) == 1;
            DisplayUploadDialog = PlayerPrefs.GetInt("AST_DisplayUploadDialog", 0) == 1;
        }

        public void Save(bool triggerSettingsChange = false)
        {
            PlayerPrefs.SetInt("AST_LegacyVersionCheck", LegacyVersionCheck ? 1 : 0);
            PlayerPrefs.SetInt("AST_UploadVersionCheck", UploadVersionCheck ? 1 : 0);
            PlayerPrefs.SetInt("AST_EnableSymlinkSupport", EnableSymlinkSupport ? 1 : 0);
            PlayerPrefs.SetInt("AST_UseLegacyExporting", UseLegacyExporting ? 1 : 0);
            PlayerPrefs.SetInt("AST_DisplayUploadDialog", DisplayUploadDialog ? 1 : 0);
            PlayerPrefs.Save();

            if(triggerSettingsChange)
                OnSettingsChange?.Invoke();
        }

        /// <summary>
        /// Check if legacy Asset Store Tools are in the Project
        /// </summary>
        public bool LegacyVersionCheck;

        /// <summary>
        /// Check if the package has been uploader from a correct Unity version at least once
        /// </summary>
        public bool UploadVersionCheck;

        /// <summary>
        /// Enables Junction symlink support
        /// </summary>
        public bool EnableSymlinkSupport;

        /// <summary>
        /// Enables legacy exporting for Folder Upload workflow
        /// </summary>
        public bool UseLegacyExporting;
        
        /// <summary>
        /// Enables DisplayDialog to be shown after the uploading ends
        /// </summary>
        public bool DisplayUploadDialog;
    }
    
    internal class ASToolsPreferencesProvider : SettingsProvider
    {
        private const string SettingsPath = "Project/Asset Store Tools";
        
        private class Styles
        {
            public static readonly GUIContent LegacyVersionCheckLabel = EditorGUIUtility.TrTextContent("Legacy ASTools Check", "Enable Legacy Asset Store Tools version checking.");
            public static readonly GUIContent UploadVersionCheckLabel = EditorGUIUtility.TrTextContent("Upload Version Check", "Check if the package has been uploader from a correct Unity version at least once.");
            public static readonly GUIContent EnableSymlinkSupportLabel = EditorGUIUtility.TrTextContent("Enable Symlink Support", "Enable Junction Symlink support. Note: folder selection validation will take longer.");
            public static readonly GUIContent UseLegacyExportingLabel = EditorGUIUtility.TrTextContent("Use Legacy Exporting", "Enabling this option uses native Unity methods when exporting packages for the Folder Upload workflow.\nNote: individual package dependency selection when choosing to 'Include Package Manifest' is unavailable when this option is enabled.");
            public static readonly GUIContent DisplayUploadDialogLabel = EditorGUIUtility.TrTextContent("Display Upload Dialog", "Show a DisplayDialog after the package uploading has finished.");
        }

        public static void OpenSettings()
        {
            SettingsService.OpenProjectSettings(SettingsPath);
        }

        private ASToolsPreferencesProvider(string path, SettingsScope scopes, IEnumerable<string> keywords = null)
            : base(path, scopes, keywords) { }
        
        public override void OnGUI(string searchContext)
        {
            var preferences = ASToolsPreferences.Instance;
            EditorGUI.BeginChangeCheck();
            using (CreateSettingsWindowGUIScope())
            {
                preferences.LegacyVersionCheck = EditorGUILayout.Toggle(Styles.LegacyVersionCheckLabel, preferences.LegacyVersionCheck);
                preferences.UploadVersionCheck = EditorGUILayout.Toggle(Styles.UploadVersionCheckLabel, preferences.UploadVersionCheck);
                preferences.EnableSymlinkSupport = EditorGUILayout.Toggle(Styles.EnableSymlinkSupportLabel, preferences.EnableSymlinkSupport);
                preferences.UseLegacyExporting = EditorGUILayout.Toggle(Styles.UseLegacyExportingLabel, preferences.UseLegacyExporting);
                preferences.DisplayUploadDialog = EditorGUILayout.Toggle(Styles.DisplayUploadDialogLabel, preferences.DisplayUploadDialog);
            }

            if (EditorGUI.EndChangeCheck())
                ASToolsPreferences.Instance.Save(true);
        }
        
        [SettingsProvider]
        public static SettingsProvider CreateAssetStoreToolsSettingProvider()
        {
            var provider = new ASToolsPreferencesProvider(SettingsPath, SettingsScope.Project, GetSearchKeywordsFromGUIContentProperties<Styles>());
            return provider;
        }
        
        private IDisposable CreateSettingsWindowGUIScope()
        {
            var unityEditorAssembly = Assembly.GetAssembly(typeof(EditorWindow));
            var type = unityEditorAssembly.GetType("UnityEditor.SettingsWindow+GUIScope");
            return Activator.CreateInstance(type) as IDisposable;
        }
    }
}
