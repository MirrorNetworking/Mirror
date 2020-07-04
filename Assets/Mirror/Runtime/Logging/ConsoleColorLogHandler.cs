using System;
using UnityEngine;

namespace Mirror.Logging
{
    public class ConsoleColorLogHandler : ILogHandler
    {
        readonly bool showExceptionStackTrace;

        public ConsoleColorLogHandler(bool showExceptionStackTrace)
        {
            this.showExceptionStackTrace = showExceptionStackTrace;
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Exception: {exception.Message}");
            if (showExceptionStackTrace)
            {
                Console.WriteLine($"    {exception.StackTrace}");
            }
            Console.ResetColor();
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            switch (logType)
            {
                case LogType.Exception:
                case LogType.Error:
                case LogType.Assert:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LogType.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
            }

            Console.WriteLine(string.Format(format, args));
            Console.ResetColor();
        }
    }
}
