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

            logger.filterLogType = (LogType)EditorGUILayout.EnumPopup(new GUIContent(name), logger.filterLogType);
        }
    }
}
