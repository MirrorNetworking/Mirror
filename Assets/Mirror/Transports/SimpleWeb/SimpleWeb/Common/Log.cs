using System;
using UnityEngine;
using Conditional = System.Diagnostics.ConditionalAttribute;

namespace Mirror.SimpleWeb
{
    public static class Log
    {
        // The.NET console color names map to the following approximate CSS color names:

        // Black:       Black
        // Blue:        Blue
        // Cyan:        Aqua or Cyan
        // DarkBlue:    DarkBlue
        // DarkCyan:    DarkCyan
        // DarkGray:    DarkGray
        // DarkGreen:   DarkGreen
        // DarkMagenta: DarkMagenta
        // DarkRed:     DarkRed
        // DarkYellow:  DarkOrange or DarkGoldenRod
        // Gray:        Gray
        // Green:       Green
        // Magenta:     Magenta
        // Red:         Red
        // White:       White
        // Yellow:      Yellow

        // We can't use colors that are close to white or black because
        // they won't show up well in the server console or browser console

        public enum Levels
        {
            Flood,
            Verbose,
            Info,
            Warn,
            Error,
            None
        }

        public static ILogger logger = Debug.unityLogger;
        public static Levels minLogLevel = Levels.None;

        // always log Exceptions
        public static void Exception(Exception e)
        {
#if UNITY_SERVER || UNITY_WEBGL
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[SWT:Exception] {e.GetType().Name}: {e.Message}\n{e.StackTrace}\n\n");
            Console.ResetColor();
#else
            logger.Log(LogType.Exception, $"[SWT:Exception] {e.GetType().Name}: {e.Message}\n{e.StackTrace}\n\n");
#endif
        }

        [Conditional("DEBUG")]
        public static void Flood(string msg)
        {
            if (minLogLevel > Levels.Flood) return;

#if UNITY_SERVER || UNITY_WEBGL
            Console.ForegroundColor = ConsoleColor.Gray;
            logger.Log(LogType.Log, msg);
            Console.ResetColor();
#else
            logger.Log(LogType.Log, msg);
#endif
        }

        [Conditional("DEBUG")]
        public static void DumpBuffer(string label, byte[] buffer, int offset, int length)
        {
            if (minLogLevel > Levels.Flood) return;

#if UNITY_SERVER || UNITY_WEBGL
            Console.ForegroundColor = ConsoleColor.DarkBlue;
            logger.Log(LogType.Log, $"{label}: {BufferToString(buffer, offset, length)}");
            Console.ResetColor();
#else
            logger.Log(LogType.Log, $"<color=cyan>{label}: {BufferToString(buffer, offset, length)}</color>");
#endif
        }

        [Conditional("DEBUG")]
        public static void DumpBuffer(string label, ArrayBuffer arrayBuffer)
        {
            if (minLogLevel > Levels.Flood) return;

#if UNITY_SERVER || UNITY_WEBGL
            Console.ForegroundColor = ConsoleColor.DarkBlue;
            logger.Log(LogType.Log, $"{label}: {BufferToString(arrayBuffer.array, 0, arrayBuffer.count)}");
            Console.ResetColor();
#else
            logger.Log(LogType.Log, $"<color=cyan>{label}: {BufferToString(arrayBuffer.array, 0, arrayBuffer.count)}</color>");
#endif
        }

        public static void Verbose(string msg)
        {
            if (minLogLevel > Levels.Verbose) return;

#if DEBUG
            // Debug builds and Unity Editor
            logger.Log(LogType.Log, msg);
#else
            // Server or WebGL
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(msg);
            Console.ResetColor();
#endif
        }

        public static void Info(string msg)
        {
            if (minLogLevel > Levels.Info) return;

#if DEBUG
            // Debug builds and Unity Editor
            logger.Log(LogType.Log, msg);
#else
            // Server or WebGL
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(msg);
            Console.ResetColor();
#endif
        }

        public static void InfoException(Exception e)
        {
            if (minLogLevel > Levels.Info) return;

#if DEBUG
            // Debug builds and Unity Editor
            logger.Log(LogType.Exception, e.Message);
#else
            // Server or WebGL
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine(e.Message);
            Console.ResetColor();
#endif
        }

        public static void Warn(string msg)
        {
            if (minLogLevel > Levels.Warn) return;

#if DEBUG
            // Debug builds and Unity Editor
            logger.Log(LogType.Warning, msg);
#else
            // Server or WebGL
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(msg);
            Console.ResetColor();
#endif
        }

        public static void Error(string msg)
        {
            if (minLogLevel > Levels.Error) return;

#if DEBUG
            // Debug builds and Unity Editor
            logger.Log(LogType.Error, msg);
#else
            // Server or WebGL
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg);
            Console.ResetColor();
#endif
        }

        public static string BufferToString(byte[] buffer, int offset = 0, int? length = null) => BitConverter.ToString(buffer, offset, length ?? buffer.Length);
    }
}
