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
                filterLogType = defaultLogLevel
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

        public static bool WarnEnabled(this ILogger logger) => logger.IsLogTypeAllowed(LogType.Warning);

        public static bool ErrorEnabled(this ILogger logger) => logger.IsLogTypeAllowed(LogType.Warning);
    }
}
