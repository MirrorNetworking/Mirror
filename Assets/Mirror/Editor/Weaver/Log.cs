using System;

namespace Mirror.Weaver
{
    public static class Log
    {
        public static Action<string> WarningMethod;
        public static Action<string> ErrorMethod;

        public static void Warning(string msg)
        {
            WarningMethod(msg);
        }

        public static void Error(string msg)
        {
            ErrorMethod(msg);
        }
    }
}
