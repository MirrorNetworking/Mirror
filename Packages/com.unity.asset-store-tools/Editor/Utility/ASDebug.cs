using UnityEditor;
using UnityEngine;

namespace AssetStoreTools.Utility
{
    internal static class ASDebug
    {
        private enum LogType
        {
            Log,
            Warning,
            Error
        }

        private static bool s_debugModeEnabled = EditorPrefs.GetBool("ASTDebugMode");

        public static bool DebugModeEnabled
        {
            get => s_debugModeEnabled;
            set
            {
                s_debugModeEnabled = value;
                EditorPrefs.SetBool("ASTDebugMode", value);
            }
        }

        public static void Log(object message)
        {
            LogMessage(message, LogType.Log);
        }

        public static void LogWarning(object message)
        {
            LogMessage(message, LogType.Warning);
        }

        public static void LogError(object message)
        {
            LogMessage(message, LogType.Error);
        }

        private static void LogMessage(object message, LogType type)
        {
            if (!DebugModeEnabled) 
                return;

            switch (type)
            {
                case LogType.Log:
                    Debug.Log(message);
                    break;
                case LogType.Warning:
                    Debug.LogWarning(message);
                    break;
                case LogType.Error:
                    Debug.LogError(message);
                    break;
                default:
                    Debug.Log(message);
                    break;
            }
        }
    }
}
