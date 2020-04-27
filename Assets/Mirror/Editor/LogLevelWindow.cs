using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mirror
{
    [CustomEditor(typeof(LogSettings), true)]
    public class LogSettingEditor : Editor
    {
        private static Dictionary<string, LogType> levels = new Dictionary<string, LogType>();
        
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
            SetLevels();

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

        private void SetLevels()
        {
            foreach (KeyValuePair<string, LogType> kvp in levels)
            {
                LogFactory.GetLogger(kvp.Key).filterLogType = kvp.Value;
            }
        }

        void DrawLoggerField(string loggerName, ILogger logger)
        {
            logger.filterLogType = (LogType)EditorGUILayout.EnumPopup(new GUIContent(loggerName), logger.filterLogType);
            levels[loggerName] = logger.filterLogType;
        }

        #endregion

        #region Log settings persistence

        private void SaveLevels()
        {
            LogSettings settings = target as LogSettings;

            Undo.RecordObject(settings, "Update log settings");
            settings.Levels = new List<LogSettings.Level>();
            settings.Levels.AddRange(LogFactory.loggers.Select(kvp => new LogSettings.Level { Name = kvp.Key, level = kvp.Value.filterLogType }));
        }

        #endregion
    }
}
