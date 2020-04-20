using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mirror.Logging
{
    public static class LogSettingsGUI
    {
        public static LogSettings DrawCreateNewButton()
        {
            if (GUILayout.Button("Create New"))
            {
                ScriptableObjectUtility.CreateAsset<LogSettings>(nameof(LogSettings));
            }

            return null;
        }
        public static void DrawLogFactoryDictionary(LogSettings settings)
        {
            using (EditorGUI.ChangeCheckScope scope = new EditorGUI.ChangeCheckScope())
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

        static void DrawLoggerField(KeyValuePair<string, ILogger> item)
        {
            ILogger logger = item.Value;
            string name = item.Key;

            logger.filterLogType = (LogType)EditorGUILayout.EnumPopup(new GUIContent(name), logger.filterLogType);
        }
    }
}
