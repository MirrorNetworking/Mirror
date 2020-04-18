using UnityEditor;

namespace Mirror.Logging
{
    [CustomEditor(typeof(LogSettingsConfig))]
    public class LogSettingsConfigEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("settings"));
            serializedObject.ApplyModifiedProperties();

            LogSettingsConfig target = this.target as LogSettingsConfig;
            if (target.settings != null)
            {
                LogFactoryGUI.DrawLogFactoryDictionary(target.settings);
            }
        }
    }
}
