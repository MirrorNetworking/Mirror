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

        [Serializable]
        public struct LogSetting
        {
            public string logger;
            public LogType logType;
        }

        private void LoadSavedLevels()
        {
            string levelsJson = EditorPrefs.GetString("LogLevels");
            LogSetting[] settings =  FromJson<LogSetting>(levelsJson);
            SetLoggers(settings ?? new LogSetting[] { });
        }

        private void SaveLevels()
        {
            LogSetting[] settings = new LogSetting[LogFactory.loggers.Count()];

            int i = 0;
            foreach (KeyValuePair<string, ILogger> item in LogFactory.loggers)
            {
                settings[i++] = new LogSetting
                {
                    logger = item.Key,
                    logType = item.Value.filterLogType
                };
            }

            string levelsJSon = ToJson(settings);
            EditorPrefs.SetString("LogLevels", levelsJSon);
        }

        public static T[] FromJson<T>(string json)
        {
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(json);
            return wrapper.Items;
        }

        public static string ToJson<T>(T[] array)
        {
            Wrapper<T> wrapper = new Wrapper<T>();
            wrapper.Items = array;
            return JsonUtility.ToJson(wrapper);
        }

        [Serializable]
        private class Wrapper<T>
        {
            public T[] Items;
        }
    }
}
