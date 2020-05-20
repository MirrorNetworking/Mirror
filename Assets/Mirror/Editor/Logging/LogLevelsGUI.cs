using System.Collections.Generic;
using Mirror.Logging;
using UnityEditor;
using UnityEngine;

namespace Mirror.EditorScripts.Logging
{
    public static class LogLevelsGUI
    {
        public static LogSettings DrawCreateNewButton()
        {
            if (GUILayout.Button("Create New"))
            {
                return ScriptableObjectUtility.CreateAsset<LogSettings>(nameof(LogSettings));
            }

            return null;
        }

        public static void DrawLogFactoryDictionary(LogSettings settings)
        {
            using (EditorGUI.ChangeCheckScope scope = new EditorGUI.ChangeCheckScope())
            {
                if (LogFactory.loggers.Count == 0)
                {
                    EditorGUILayout.LabelField("No Keys found in LogFactory.loggers\nPlay the game for default log values to be added to LogFactory", EditorStyles.wordWrappedLabel);
                }
                else
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Logging Components", EditorStyles.boldLabel);

                    foreach (KeyValuePair<string, ILogger> item in LogFactory.loggers)
                    {
                        DrawLoggerField(item);
                    }

                    if (scope.changed)
                    {
                        settings.SaveFromDictionary(LogFactory.loggers);
                    }
                }
            }
        }

        static void DrawLoggerField(KeyValuePair<string, ILogger> item)
        {
            ILogger logger = item.Value;
            string name = item.Key;

            const float fieldWidth = 100f;
            const float inspectorMargin = 25f;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent(ObjectNames.NicifyVariableName(name)), GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - fieldWidth - inspectorMargin));
                logger.filterLogType = (LogType)EditorGUILayout.EnumPopup(logger.filterLogType, GUILayout.Width(fieldWidth));
            }
        }
    }
}
