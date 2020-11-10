using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mirror
{
    [CustomEditor(typeof(LogSettings), true)]
    public class LogSettingEditor : Editor
    {
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

            foreach (KeyValuePair<string, ILogger> item in LogFactory.loggers)
            {
                DrawLoggerField(item.Key, item.Value);               
            }
            EditorGUILayout.EndVertical();

            if (EditorGUI.EndChangeCheck() || !EditorApplication.isPlaying)
            {
                SaveLevels();
            }
        }

        void DrawLoggerField(string loggerName, ILogger logger)
        {
            logger.filterLogType = (LogType)EditorGUILayout.EnumPopup(new GUIContent(loggerName), logger.filterLogType);
        }

        #endregion

        #region Log settings persistence

        private void SaveLevels()
        {
            var settings = target as LogSettings;

            Undo.RecordObject(settings, "Update log settings");
            settings.Levels = new List<LogSettings.Level>();
            settings.Levels.AddRange(LogFactory.loggers.Select(kvp => new LogSettings.Level { Name = kvp.Key, level = kvp.Value.filterLogType }));
        }

        #endregion
    }

    public struct LogLevelContainer
    {
        public List<LogSettings.Level> levels;

        public LogLevelContainer(List<LogSettings.Level> levels)
        {
            this.levels = levels;
        }
    }

    [InitializeOnLoad]
    public static class LogSettingsSaver
    {
        static LogSettingsSaver()
        {
            EditorApplication.playModeStateChanged += OnChangePlayModeState;
            Load();
        }

        private static void OnChangePlayModeState(PlayModeStateChange state)
        {
            // Create backups of the scenes before you enter play mode, because this thing is pretty destructive and you can lose work if it goes wrong.
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Save();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                Load();
            }
        }

        private static void Load()
        {
            string leveljson = EditorPrefs.GetString("Log Levels", "{\"levels\": [] }");

            LogLevelContainer levelContainer = JsonUtility.FromJson<LogLevelContainer>(leveljson);
            List<LogSettings.Level> levels = levelContainer.levels;

            foreach (LogSettings.Level level in levels)
            {
                LogFactory.GetLogger(level.Name).filterLogType = level.level;
            }
        }

        private static void Save()
        {
            var levels = LogFactory.loggers.Select(kvp => new LogSettings.Level { Name = kvp.Key, level = kvp.Value.filterLogType }).ToList();

            var levelContainer = new LogLevelContainer(levels);

            string leveljson = JsonUtility.ToJson(levelContainer);

            EditorPrefs.SetString("Log Levels", leveljson);
        }
    }
}
