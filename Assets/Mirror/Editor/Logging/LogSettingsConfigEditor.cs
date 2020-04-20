using UnityEditor;

namespace Mirror.Logging
{
    [CustomEditor(typeof(LogSettingsConfig))]
    public class LogSettingsConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();


            LogSettingsConfig target = this.target as LogSettingsConfig;

            if (target.settings == null)
            {
                LogSettings newSettings = LogSettingsGUI.DrawCreateNewButton();
                if (newSettings != null)
                {
                    SerializedProperty settingsProp = serializedObject.FindProperty("settings");
                    settingsProp.objectReferenceValue = newSettings;
                }
            }
            else
            {
                LogSettingsGUI.DrawLogFactoryDictionary(target.settings);
            }
        }
    }
}
