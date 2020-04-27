using UnityEditor;
using UnityEngine;

namespace Mirror.EditorScripts.Logging
{
    public class LogLevelWindow : EditorWindow
    {
        void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(new GUIContent("Mirror Log Levels"), EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.inspectorDefaultMargins);
            LogLevelsGUI.DrawLogFactoryDictionary();
            EditorGUILayout.EndVertical();
        }

        [MenuItem("Window/Analysis/Mirror Log Levels", priority = 20002)]
        public static void ShowWindow()
        {
            LogLevelWindow window = GetWindow<LogLevelWindow>();
            window.titleContent = new GUIContent("Mirror Log levels");
            window.Show();
        }
    }
}
