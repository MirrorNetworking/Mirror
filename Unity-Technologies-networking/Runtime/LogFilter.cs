using System;

#if ENABLE_UNET
namespace UnityEngine.Networking
{
    public class LogFilter
    {
        // this only exists for inspector UI?!
        public enum FilterLevel
        {
            Developer = 0,
            Debug = 1,
            Info = 2,
            Warn = 3,
            Error = 4,
            SetInScripting = -1
        };

        static public FilterLevel currentLogLevel = FilterLevel.Info;

        static internal bool logDev { get { return currentLogLevel <= FilterLevel.Developer; } }
        static public bool logDebug { get { return currentLogLevel <= FilterLevel.Debug; } }
        static public bool logInfo  { get { return currentLogLevel <= FilterLevel.Info; } }
        static public bool logWarn  { get { return currentLogLevel <= FilterLevel.Warn; } }
        static public bool logError  { get { return currentLogLevel <= FilterLevel.Error; } }
    }
}
#endif //ENABLE_UNET
