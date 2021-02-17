using System;
using UnityEngine;
using UnityEngine.Networking;

namespace Mirror.Cloud
{
    public static class Logger
    {
        public static bool VerboseLogging = false;

        public static void LogRequest(string page, string method, bool hasJson, string json)
        {
            if (hasJson)
            {
                Debug.LogFormat("Request: {0} {1} {2}", method, page, json);
            }
            else
            {
                Debug.LogFormat("Request: {0} {1}", method, page);
            }
        }

        public static void LogResponse(UnityWebRequest statusRequest)
        {
            long code = statusRequest.responseCode;

            string format = "Response: {0} {1} {2} {3}";
            // we split path like this to make sure api key doesn't leak
            Uri uri = new Uri(statusRequest.url);
            string path = string.Join("", uri.Segments);
            string msg = string.Format(format, statusRequest.method, code, path, statusRequest.downloadHandler.text);
            Debug.Log(msg);

            if (!string.IsNullOrEmpty(statusRequest.error))
            {
                msg = string.Format("WEB REQUEST ERROR: {0}", statusRequest.error);
                Debug.LogError(msg);
            }
        }

        internal static void Log(string msg)
        {
            Debug.Log(msg);
        }

        internal static void LogWarning(string msg)
        {
            Debug.LogWarning(msg);
        }

        internal static void LogError(string msg)
        {
            Debug.LogError(msg);
        }

        internal static void Verbose(string msg)
        {
            if (VerboseLogging)
                Debug.Log(msg);
        }
    }
}
