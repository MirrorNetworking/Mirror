using UnityEditor;
using UnityEngine;

namespace Mirror.Logging
{
    [CustomEditor(typeof(LogSettings))]
    public class LogSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            CurrentScriptField();

            LogSettingsGUI.DrawLogFactoryDictionary(target as LogSettings);
        }

        public void CurrentScriptField()
        {
            GUI.enabled = false;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            GUI.enabled = true;
        }
    }
}
