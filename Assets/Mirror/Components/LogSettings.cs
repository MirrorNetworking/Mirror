using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Mirror
{
    [ExecuteInEditMode]
    public class LogSettings : MonoBehaviour
    {
        [Serializable]
        public struct LoggerSetting
        {
            public string Name;
            public LogType level;
        };

        [SerializeField]
        public List<LoggerSetting> settings = new List<LoggerSetting>();


        // Start is called before the first frame update
        void Start()
        {
            UpdateLoggers();
        }

        public void OnValidate()
        {
            UpdateLoggers();
        }

        public void UpdateLoggers()
        {
            // set the logger settings for anyone we have configured
            HashSet<string> loggerNames = new HashSet<string>(LogFactory.Loggers);
            foreach (var loggerSetting in settings)
            {
                loggerNames.Remove(loggerSetting.Name);
                var logger = LogFactory.GetLogger(loggerSetting.Name);
                logger.filterLogType = loggerSetting.level;
            }

            // discover any new logger
            foreach (var newLoggerName in loggerNames)
            {
                LoggerSetting newSetting = new LoggerSetting()
                {
                    Name = newLoggerName,
                    level = LogFactory.GetLogger(newLoggerName).filterLogType
                };
                settings.Add(newSetting);
            }
        }
    }
}
