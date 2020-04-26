using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mirror
{
    [CustomEditor(typeof(LogSettings), true)]
    public class LogSettingEditor : Editor
    {
        void OnEnable()
        {
            // Setup the SerializedProperties.
            EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
        }

        private void EditorApplication_playModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                SaveLevels();
            }
        }

        #region GUI
        public override void OnInspectorGUI()
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

        static void DrawLoggerField(string loggerName, ILogger logger)
        {
            logger.filterLogType = (LogType)EditorGUILayout.EnumPopup(new GUIContent(loggerName), logger.filterLogType);
        }

        #endregion

        #region Log settings persistence

        private void SetLoggers(IEnumerable<LogSettings.Level> savedLevels)
        {
            foreach (var setting in savedLevels)
            {
                if (setting.Name != null)
                {
                    ILogger logger = LogFactory.GetLogger(setting.Name);
                    logger.filterLogType = setting.level;
                }
            }
        }

        private void LoadSavedLevels()
        {
            string levelsJson = EditorPrefs.GetString("LogLevels");
            var settings = FromJson<LogSettings.Level>(levelsJson);
            SetLoggers(settings ?? new LogSettings.Level[] { });
        }

        private void SaveLevels()
        {
            // save in EditorPrefs
            SaveInEditorPrefs();
            SaveInGameObject();
        }

        private void SaveInGameObject()
        {
            LogSettings setting = target as LogSettings;

            if (target == null)
                return;

            setting.Levels = new List<LogSettings.Level>();

            foreach (KeyValuePair<string, ILogger> item in LogFactory.loggers)
            {
                setting.Levels.Add(new LogSettings.Level
                {
                    Name = item.Key,
                    level = item.Value.filterLogType
                });
            }

            Undo.RecordObject(target, "Update log settings");
        }


        private static void SaveInEditorPrefs()
        {
            LogSettings.Level[] settings = new LogSettings.Level[LogFactory.loggers.Count()];

            int i = 0;
            foreach (KeyValuePair<string, ILogger> item in LogFactory.loggers)
            {
                settings[i++] = new LogSettings.Level
                {
                    Name = item.Key,
                    level = item.Value.filterLogType
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

        #endregion
    }
}
