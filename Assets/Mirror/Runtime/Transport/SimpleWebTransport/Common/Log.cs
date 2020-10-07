using System;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Debug = UnityEngine.Debug;

namespace Mirror.SimpleWeb
{
    public static class Log
    {
        public enum Levels
        {
            none = 0,
            error = 1,
            info = 2,
            verbose = 3,
        }

        public static Levels level = Levels.none;

        [Conditional("DEBUG")]
        public static void Verbose(string msg)
        {
            if (level < Levels.verbose)
                return;

            Debug.Log($"Verbose: <color=blue>{msg}</color>");
        }

        [Conditional("DEBUG")]
        public static void Verbose(string msg, bool showColor)
        {
            if (level < Levels.verbose)
                return;

            if (showColor)
                Verbose(msg);
            else
                Debug.Log($"Verbose: {msg}");
        }

        [Conditional("DEBUG")]
        public static void DumpBuffer(byte[] buffer, int offset, int length)
        {
            if (level < Levels.verbose)
                return;

            string text = BitConverter.ToString(buffer, offset, length);
            Verbose(text);
        }

        [Conditional("DEBUG")]
        public static void DumpBuffer(string label, byte[] buffer, int offset, int length)
        {
            if (level < Levels.verbose)
                return;

            string text = BitConverter.ToString(buffer, offset, length);
            Verbose($"{label}: {text}");
        }

        [Conditional("DEBUG")]
        public static void Info(string msg)
        {
            if (level < Levels.info)
                return;

            Debug.Log($"INFO: <color=blue>{msg}</color>");
        }

        [Conditional("DEBUG")]
        public static void Info(string msg, bool showColor)
        {
            if (level < Levels.info)
                return;

            if (showColor)
                Info(msg);
            else
                Debug.Log($"INFO: {msg}");
        }


        [Conditional("DEBUG")]
        public static void Error(string msg)
        {
            if (level < Levels.error)
                return;

            Debug.Log($"ERROR: <color=red>{msg}</color>");
        }

        [Conditional("DEBUG")]
        public static void Error(string msg, bool showColor)
        {
            if (level < Levels.error)
                return;

            if (showColor)
                Error(msg);
            else
                Debug.Log($"ERROR: {msg}");
        }
    }
}
