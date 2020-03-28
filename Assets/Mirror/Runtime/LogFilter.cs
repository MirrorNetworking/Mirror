using UnityEngine;

namespace Mirror
{
    public static class LogFilter
    {
        public static bool Debug = false;
    }
    public static class MirrorLog
    {
        /// <summary>
        /// Use to Override Default Unity logger
        /// </summary>
        public static ILogHandler Logger = Debug.unityLogger;

        [System.Diagnostics.Conditional("DEBUG")]
        public static void DebugLog(string msg, params object[] args)
        {
            if (LogFilter.Debug)
            {
                Logger.LogFormat(LogType.Log, null, msg, args);
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void DebugLogWarning(string msg, params object[] args)
        {
            if (LogFilter.Debug)
            {
                Logger.LogFormat(LogType.Warning, null, msg, args);
            }
        }


        public static void Log(string msg, params object[] args)
        {
            Logger.LogFormat(LogType.Log, null, msg, args);
        }

        public static void LogWarning(string msg, params object[] args)
        {
            Logger.LogFormat(LogType.Warning, null, msg, args);
        }
        public static void LogError(string msg, params object[] args)
        {
            Logger.LogFormat(LogType.Error, null, msg, args);
        }
    }
}
