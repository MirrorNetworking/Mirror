using Mirror.Logging;
using UnityEditor;
using UnityEngine;

namespace Mirror.EditorScripts.Logging
{
    public class LogLevelWindow : EditorWindow
    {
        [SerializeField] LogSettings settings = null;
        SerializedObject serializedObject;
        SerializedProperty settingsProp;

        void OnEnable()
        {
            serializedObject = new SerializedObject(this);
            settingsProp = serializedObject.FindProperty(nameof(settings));

            LogSettings existingSettings = EditorLogSettingsLoader.FindLogSettings();
            if (existingSettings != null)
            {
                settingsProp.objectReferenceValue = existingSettings;
                serializedObject.ApplyModifiedProperties();
            }
        }

        void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(new GUIContent("Mirror Log Levels"), EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.inspectorDefaultMargins);

            serializedObject.Update();
            EditorGUILayout.PropertyField(settingsProp);
            serializedObject.ApplyModifiedProperties();

            if (settings == null)
            {
                LogSettings newSettings = LogLevelsGUI.DrawCreateNewButton();
                if (newSettings != null)
                {
                    settingsProp.objectReferenceValue = newSettings;
                    serializedObject.ApplyModifiedProperties();
                }
            }
            else
            {
                LogLevelsGUI.DrawLogFactoryDictionary(settings);
            }

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
