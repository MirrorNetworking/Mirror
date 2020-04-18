using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mirror.Logging
{
    [Serializable]
    public class LogSettingsJson
    {
        public List<Logger> loglevels = new List<Logger>();

        [Serializable]
        public struct Logger
        {
            public string name;
            public LogType logLevel;
        }
    }

    public static class LogSettingsSaver
    {
        public static string FileName => "MirrorLogSettings.json";

        public static string GetFullPath() => Path.Combine(Application.persistentDataPath, FileName);

        public static void Save(LogSettingsJson logSettings)
        {
            string path = GetFullPath();
            try
            {
                string json = JsonUtility.ToJson(logSettings);
                File.WriteAllText(path, json);
            }
            catch
            {
                Debug.LogWarning("Mirror Could not Save LogSettingsJson to path: " + path);
            }
        }

        public static LogSettingsJson Load()
        {
            string path = GetFullPath();
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    return JsonUtility.FromJson<LogSettingsJson>(json);
                }
            }
            catch
            {
                Debug.LogWarning("Mirror Could not load LogSettingsJson at path: " + path);
            }

            // if file not found or Exception
            return new LogSettingsJson();
        }

        public static void SaveFromDictionary(Dictionary<string, ILogger> dictionary)
        {
            LogSettingsJson settings = new LogSettingsJson();
            foreach (KeyValuePair<string, ILogger> kvp in dictionary)
            {
                settings.loglevels.Add(new LogSettingsJson.Logger { name = kvp.Key, logLevel = kvp.Value.filterLogType });
            }

            Save(settings);
        }

        public static void LoadIntoDictionary(Dictionary<string, ILogger> dictionary)
        {
            LogSettingsJson settings = Load();

            foreach (LogSettingsJson.Logger logLevel in settings.loglevels)
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
