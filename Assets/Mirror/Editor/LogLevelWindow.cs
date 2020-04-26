using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mirror
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

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginVertical(EditorStyles.inspectorDefaultMargins);
            LoadSavedLevels();

            foreach (KeyValuePair<string, ILogger> item in LogFactory.loggers)
            {
                DrawLoggerField(item.Key, item.Value);
            }
            EditorGUILayout.EndVertical();

            if (EditorGUI.EndChangeCheck())
            {
                SaveLevels();
            }
        }

        private void SetLoggers(IEnumerable<LogSetting> savedLevels)
        {
            foreach (LogSetting setting in savedLevels)
            {
                ILogger logger = LogFactory.GetLogger(setting.logger);
                logger.filterLogType = setting.logType;
            }
        }

        static void DrawLoggerField(string loggerName, ILogger logger)
        {
            logger.filterLogType = (LogType)EditorGUILayout.EnumPopup(new GUIContent(loggerName), logger.filterLogType);
        }

        [MenuItem("Window/Analysis/Mirror Log Levels", priority = 20002)]
        public static void ShowWindow()
        {
            LogLevelWindow window = GetWindow<LogLevelWindow>();
            window.titleContent = new GUIContent("Mirror Log levels");
            window.Show();
        }

        public struct LogSetting
        {
            public string logger;
            public LogType logType;
        }

        private void LoadSavedLevels()
        {
            string levelsJson = EditorPrefs.GetString("LogLevels");
            LogSetting[] settings =  JsonUtility.FromJson<LogSetting[]>(levelsJson);
            SetLoggers(settings ?? new LogSetting[] { });
        }

        private void SaveLevels()
        {
            List<LogSetting> settings = new List<LogSetting>();

            foreach (KeyValuePair<string, ILogger> item in LogFactory.loggers)
            {
                settings.Add(new LogSetting
                {
                    logger = item.Key,
                    logType = item.Value.filterLogType
                });
            }

            string levelsJSon = JsonUtility.ToJson(settings);
            EditorPrefs.SetString("LogLevels", levelsJSon);
        }
    }
}
