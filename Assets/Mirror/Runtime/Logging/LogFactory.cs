using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public static class LogFactory
    {
        internal static readonly SortedDictionary<string, ILogger> loggers = new SortedDictionary<string, ILogger>();

        public static SortedDictionary<string, ILogger>.ValueCollection AllLoggers => loggers.Values;

        /// <summary>
        /// logHandler used for new loggers
        /// </summary>
        static ILogHandler defaultLogHandler = Debug.unityLogger;

        /// <summary>
        /// if true sets all log level to LogType.Log
        /// </summary>
        static bool debugMode = false;

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

            logger = new Logger(defaultLogHandler)
            {
                // by default, log warnings and up
                filterLogType = debugMode ? LogType.Log : defaultLogLevel
            };

            loggers[loggerName] = logger;
            return logger;
        }

        /// <summary>
        /// Makes all log levels LogType.Log, this is so that NetworkManger.showDebugMessages can still be used
        /// </summary>
        public static void EnableDebugMode()
        {
            debugMode = true;

            foreach (ILogger logger in loggers.Values)
            {
                logger.filterLogType = LogType.Log;
            }
        }

        /// <summary>
        /// Replacing log handler for all existing loggers and sets defaultLogHandler for new loggers
        /// </summary>
        /// <param name="logHandler"></param>
        public static void ReplaceLogHandler(ILogHandler logHandler)
        {
            defaultLogHandler = logHandler;

            foreach (ILogger logger in loggers.Values)
            {
                logger.logHandler = logHandler;
            }
        }
    }


    public static class ILoggerExtensions
    {
        public static void LogError(this ILogger logger, object message)
        {
            logger.Log(LogType.Error, message);
        }

        public static void Assert(this ILogger logger, bool condition, string message)
        {
            if (!condition)
                logger.Log(LogType.Assert, message);
        }

        public static void LogWarning(this ILogger logger, object message)
        {
            logger.Log(LogType.Warning, message);
        }

        public static bool LogEnabled(this ILogger logger) => logger.IsLogTypeAllowed(LogType.Log);
        public static bool WarnEnabled(this ILogger logger) => logger.IsLogTypeAllowed(LogType.Warning);
        public static bool ErrorEnabled(this ILogger logger) => logger.IsLogTypeAllowed(LogType.Error);
    }
}
