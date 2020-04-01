using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mirror
{
    public class LogLevelWindow : EditorWindow
    {
        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(new GUIContent("Mirror Log Levels"), EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.inspectorDefaultMargins);
            foreach (KeyValuePair<string, ILogger> item in LogFactory.loggers)
            {
                drawLoggerField(item);
            }
            EditorGUILayout.EndVertical();
        }

        private static void drawLoggerField(KeyValuePair<string, ILogger> item)
        {
            ILogger logger = item.Value;
            string name = item.Key;

            logger.filterLogType = (LogType)EditorGUILayout.EnumPopup(new GUIContent(name), logger.filterLogType);
        }


        [MenuItem("Window/Mirror Log Levels", priority = 20002)]
        public static void ShowWindow()
        {
            LogLevelWindow window = GetWindow<LogLevelWindow>();
            window.titleContent = new GUIContent("Mirror Log levels");
            window.Show();
        }
    }
}
