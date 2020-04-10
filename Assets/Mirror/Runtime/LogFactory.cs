using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public static class LogFactory
    {
        private static readonly Dictionary<string, ILogger> loggers = new Dictionary<string, ILogger>();

        public static ILogger GetLogger<T>()
        {
            return GetLogger(typeof(T).Name);
        }

        public static ILogger GetLogger(System.Type type)
        {
            return GetLogger(type.Name);
        }

        public static ILogger GetLogger(string loggerName)
        {
            if (loggers.TryGetValue(loggerName, out ILogger logger ))
            {
                return logger;
            }

            logger = new Logger(Debug.unityLogger)
            {
                // by default, log warnings and up
                filterLogType = LogType.Warning
            };

            loggers[loggerName] = logger;
            return logger;
        }
    }


    public static class ILoggerExtensions
    {
        public static void LogError(this ILogger logger, object message)
        {
            logger.LogError(null, message);
        }

        public static void LogWarning(this ILogger logger, object message)
        {
            logger.LogWarning(null, message);
        }

        public static bool LogEnabled(this ILogger logger) => logger.IsLogTypeAllowed(LogType.Log);
    }
}