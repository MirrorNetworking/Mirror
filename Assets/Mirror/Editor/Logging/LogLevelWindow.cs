using Mirror.Logging;
using UnityEditor;
using UnityEngine;

namespace Mirror.EditorScripts.Logging
{
    public class LogLevelWindow : EditorWindow
    {
        [Header("Log Settings Asset")]
        [SerializeField] LogSettings settings = null;

        SerializedObject serializedObject;
        SerializedProperty settingsProp;
        Vector2 dictionaryScrollPosition;

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
            using (EditorGUILayout.ScrollViewScope scrollScope = new EditorGUILayout.ScrollViewScope(dictionaryScrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar))
            {
                dictionaryScrollPosition = scrollScope.scrollPosition;

                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
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
                    }
                }
            }
        }

        [MenuItem("Window/Analysis/Mirror Log Levels", priority = 20002)]
        public static void ShowWindow()
        {
            LogLevelWindow window = GetWindow<LogLevelWindow>();
            window.minSize = new Vector2(200, 100);
            window.titleContent = new GUIContent("Mirror Log Levels");
            window.Show();
        }
    }
}
