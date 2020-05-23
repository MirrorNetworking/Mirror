using Mirror.Logging;
using UnityEditor;

namespace Mirror.EditorScripts.Logging
{
    [CustomEditor(typeof(NetworkLogSettings))]
    public class NetworkLogSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            NetworkLogSettings target = this.target as NetworkLogSettings;

            if (target.settings == null)
            {
                LogSettings newSettings = LogLevelsGUI.DrawCreateNewButton();
                if (newSettings != null)
                {
                    SerializedProperty settingsProp = serializedObject.FindProperty("settings");
                    settingsProp.objectReferenceValue = newSettings;
                    serializedObject.ApplyModifiedProperties();
                }
            }
            else
            {
                LogLevelsGUI.DrawLogFactoryDictionary(target.settings);
            }
        }
    }
}
