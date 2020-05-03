using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public static class LogFactory
    {
        internal static readonly Dictionary<string, ILogger> loggers = new Dictionary<string, ILogger>();

        public static ILogger GetLogger<T>(LogType defaultLogLevel = LogType.Warning)
        {
            return GetLogger(typeof(T).Name, defaultLogLevel);
        }

        public static ILogger GetLogger(System.Type type, LogType defaultLogLevel = LogType.Warning)
        {
            return GetLogger(type.Name, defaultLogLevel);
        }

        public static ILogger GetLogger(string loggerName, LogType defaultLogLevel = LogType.Warning)
        {
            if (loggers.TryGetValue(loggerName, out ILogger logger))
            {
                return logger;
            }

            logger = new Logger(Debug.unityLogger)
            {
                // by default, log warnings and up
                filterLogType = debugMode ? LogType.Log : defaultLogLevel
            };

            loggers[loggerName] = logger;
            return logger;
        }


        static bool debugMode = false;
        /// <summary>
        /// Makes all log levels LogType.Log, this is so that NetworkManger.showDebugMessages can still be used
        /// </summary>
        internal static void EnableDebugMode()
        {
            debugMode = true;

            foreach (KeyValuePair<string, ILogger> kvp in loggers)
            {
                kvp.Value.filterLogType = LogType.Log;
            }
        }
    }


    public static class ILoggerExtensions
    {
        public static void LogError(this ILogger logger, object message)
        {
            logger.Log(LogType.Error, message);
        }

        public static void LogWarning(this ILogger logger, object message)
        {
            logger.Log(LogType.Warning, message);
        }

        public static bool LogEnabled(this ILogger logger) => logger.IsLogTypeAllowed(LogType.Log);
    }
}
