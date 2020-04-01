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

        public static ILogger GetLogger(string loggerName)
        {
            if (loggers.TryGetValue(loggerName, out ILogger logger))
            {
                return logger;
            }

            ILogHandler handler = new MultiLogHandler(Debug.unityLogger);
            logger = new Logger(handler)
            {
                // by default, log warnings and up
                filterLogType = LogType.Warning
            };

            loggers[loggerName] = logger;
            return logger;
        }

        public static void AddLogHandler(ILogHandler handler)
        {
            foreach (ILogger logger in loggers.Values)
            {
                if (logger.logHandler is MultiLogHandler multiLog)
                {
                    multiLog.handlers.Add(handler);
                }
            }
        }
        public static void RemoveLogHandler(ILogHandler handler)
        {
            foreach (ILogger logger in loggers.Values)
            {
                if (logger.logHandler is MultiLogHandler multiLog)
                {
                    multiLog.handlers.Remove(handler);
                }
            }
        }
    }

    internal class MultiLogHandler : ILogHandler
    {
        public readonly List<ILogHandler> handlers;

        public MultiLogHandler(params ILogHandler[] loggers)
        {
            handlers = new List<ILogHandler>(loggers);
        }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            for (int i = 0; i < handlers.Count; i++)
            {
                handlers[i].LogFormat(logType, context, format, args);
            }
        }
        public void LogException(System.Exception exception, Object context)
        {
            for (int i = 0; i < handlers.Count; i++)
            {
                handlers[i].LogException(exception, context);
            }
        }
    }
}
