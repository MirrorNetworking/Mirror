using System;
using UnityEngine;

namespace Mirror
{
    public static class LogFilter
    {
        // this only exists for inspector UI?!
        public enum FilterLevel
        {
            Developer = 0,
            Debug = 1,
            Info = 2,
            Warn = 3,
            SetInScripting = -1
        }

        public static FilterLevel currentLogLevel = FilterLevel.Info;

        internal static bool logDev { get { return currentLogLevel <= FilterLevel.Developer; } }
        public static bool logDebug { get { return currentLogLevel <= FilterLevel.Debug; } }
        public static bool logInfo  { get { return currentLogLevel <= FilterLevel.Info; } }
        public static bool logWarn  { get { return currentLogLevel <= FilterLevel.Warn; } }
    }
}
