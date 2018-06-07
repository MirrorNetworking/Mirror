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
            Fatal = 5,
            SetInScripting = -1
        };

        internal const int Developer = 0;
        internal const int SetInScripting = -1;

        public const int Debug = 1;
        public const int Info = 2;
        public const int Warn = 3;
        public const int Error = 4;
        public const int Fatal = 5;

        [Obsolete("Use LogFilter.currentLogLevel instead")]
        static public FilterLevel current = FilterLevel.Info;

        static int s_CurrentLogLevel = Info;
        static public int currentLogLevel { get { return s_CurrentLogLevel; } set { s_CurrentLogLevel = value; } }

        static internal bool logDev { get { return s_CurrentLogLevel <= Developer; } }
        static public bool logDebug { get { return s_CurrentLogLevel <= Debug; } }
        static public bool logInfo  { get { return s_CurrentLogLevel <= Info; } }
        static public bool logWarn  { get { return s_CurrentLogLevel <= Warn; } }
        static public bool logError  { get { return s_CurrentLogLevel <= Error; } }
        static public bool logFatal { get { return s_CurrentLogLevel <= Fatal; } }
    }
}
#endif //ENABLE_UNET
