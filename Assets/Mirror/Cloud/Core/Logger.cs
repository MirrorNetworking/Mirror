using System;
using UnityEngine;
using UnityEngine.Networking;

namespace Mirror.Cloud
{
    public static class Logger
    {
        public static bool VerboseLogging = false;
        static readonly ILogger logger = LogFactory.GetLogger("MirrorCloudServices");

        public static void LogRequest(string page, string method, bool hasJson, string json)
        {
            if (hasJson)
            {
                logger.LogFormat(LogType.Log, "Request: {0} {1} {2}", method, page, json);
            }
            else
            {
                logger.LogFormat(LogType.Log, "Request: {0} {1}", method, page);
            }
        }

        public static void LogResponse(UnityWebRequest statusRequest)
        {
            long code = statusRequest.responseCode;
            LogType logType = statusRequest.IsOk()
                ? LogType.Log
                : LogType.Error;

            string format = "Response: {0} {1} {2} {3}";
            if (logger.IsLogTypeAllowed(logType))
            {
                // we split path like this to make sure api key doesn't leak
                Uri uri = new Uri(statusRequest.url);
                string path = string.Join("", uri.Segments);
                logger.LogFormat(logType, format, statusRequest.method, code, path, statusRequest.downloadHandler.text);
            }
        }

        internal static void Log(string msg)
        {
            if (logger.LogEnabled())
                logger.Log(msg);
        }

        internal static void LogWarning(string msg)
        {
            if (logger.WarnEnabled())
                logger.LogWarning(msg);
        }

        internal static void LogError(string msg)
        {
            if (logger.ErrorEnabled())
                logger.LogError(msg);
        }

        internal static void Verbose(string msg)
        {
            if (VerboseLogging && logger.LogEnabled())
                logger.Log(msg);
        }
    }
}
