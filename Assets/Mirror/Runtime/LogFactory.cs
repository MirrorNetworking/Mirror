using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    // hold a reference to the class logger
    internal static class Loggers<T>
    {
        // note that Loggers<X>.Logger
        // would have a separate value than Loggers<Y>.Logger
        // this way we can always get teh same logger back and change it's log level
        public static ILogger Logger = new Logger(Debug.unityLogger)
        {
            // by default, log warnings and up
            filterLogType = LogType.Warning
        };
    }

    public static class LogFactory
    {
        public static ILogger GetLogger<T>()
        {
            return Loggers<T>.Logger;
        }
    }
}