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

        /// <summary>
        /// Logs all exceptions to console
        /// </summary>
        /// <param name="e">Exception to log</param>
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

        /// <summary>
        /// Logs flood to console if minLogLevel is set to Flood or lower
        /// </summary>
        /// <param name="msg">Message text to log</param>
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

        /// <summary>
        /// Logs buffer to console if minLogLevel is set to Flood or lower
        /// <para>Debug mode requrired, e.g. Unity Editor of Develpment Build</para>
        /// </summary>
        /// <param name="label">Source of the log message</param>
        /// <param name="buffer">Byte array to be logged</param>
        /// <param name="offset">starting point of byte array</param>
        /// <param name="length">number of bytes to read</param>
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

        /// <summary>
        /// Logs buffer to console if minLogLevel is set to Flood or lower
        /// <para>Debug mode requrired, e.g. Unity Editor of Develpment Build</para>
        /// </summary>
        /// <param name="label">Source of the log message</param>
        /// <param name="arrayBuffer">ArrayBuffer to show details for</param>
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

        /// <summary>
        /// Logs verbose to console if minLogLevel is set to Verbose or lower
        /// </summary>
        /// <param name="msg">Message text to log</param>
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

        /// <summary>
        /// Logs info to console if minLogLevel is set to Info or lower
        /// </summary>
        /// <param name="msg">Message text to log</param>
        /// <param name="consoleColor">Default Cyan works in server and browser consoles</param>
        public static void Info(string msg, ConsoleColor consoleColor = ConsoleColor.Cyan)
        {
            if (minLogLevel > Levels.Info) return;

#if DEBUG
            // Debug builds and Unity Editor
            logger.Log(LogType.Log, msg);
#else
            // Server or WebGL
            Console.ForegroundColor = consoleColor;
            Console.WriteLine(msg);
            Console.ResetColor();
#endif
        }

        /// <summary>
        /// Logs info to console if minLogLevel is set to Info or lower
        /// </summary>
        /// <param name="e">Exception to log</param>
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

        /// <summary>
        /// Logs info to console if minLogLevel is set to Warn or lower
        /// </summary>
        /// <param name="msg">Message text to log</param>
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

        /// <summary>
        /// Logs info to console if minLogLevel is set to Error or lower
        /// </summary>
        /// <param name="msg">Message text to log</param>
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

        /// <summary>
        /// Returns a string representation of the byte array starting from offset for length bytes
        /// </summary>
        /// <param name="buffer">Byte array to read</param>
        /// <param name="offset">starting point in the byte array</param>
        /// <param name="length">number of bytes to read from offset</param>
        /// <returns></returns>
        public static string BufferToString(byte[] buffer, int offset = 0, int? length = null) => BitConverter.ToString(buffer, offset, length ?? buffer.Length);
    }
}
