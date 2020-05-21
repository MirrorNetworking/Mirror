using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Logging
{
    public class LogSettings : ScriptableObject
    {
        public List<LoggerSettings> loglevels = new List<LoggerSettings>();

        [Serializable]
        public struct LoggerSettings
        {
            public string name;
            public LogType logLevel;
        }
    }

    public static class LogSettingsExt
    {
        public static void SaveFromDictionary(this LogSettings settings, SortedDictionary<string, ILogger> dictionary)
        {
            if (settings == null)
            {
                Debug.LogWarning("Could not SaveFromDictionary because LogSettings were null");
                return;
            }

            settings.loglevels.Clear();

            foreach (KeyValuePair<string, ILogger> kvp in dictionary)
            {
                settings.loglevels.Add(new LogSettings.LoggerSettings { name = kvp.Key, logLevel = kvp.Value.filterLogType });
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(settings);
#endif
        }

        public static void LoadIntoDictionary(this LogSettings settings, SortedDictionary<string, ILogger> dictionary)
        {
            if (settings == null)
            {
                Debug.LogWarning("Could not LoadIntoDictionary because LogSettings were null");
                return;
            }

            foreach (LogSettings.LoggerSettings logLevel in settings.loglevels)
            {
                if (dictionary.TryGetValue(logLevel.name, out ILogger logger))
                {
                    logger.filterLogType = logLevel.logLevel;
                }
                else
                {
                    logger = new Logger(Debug.unityLogger)
                    {
                        filterLogType = logLevel.logLevel
                    };

                    dictionary[logLevel.name] = logger;
                }
            }
        }
    }
}
