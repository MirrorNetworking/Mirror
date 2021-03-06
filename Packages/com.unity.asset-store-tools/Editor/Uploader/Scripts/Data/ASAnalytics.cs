using UnityEditor;
using UnityEngine.Analytics;

namespace AssetStoreTools.Uploader.Data
{
    internal static class ASAnalytics
    {
        private const int VersionId = 3;
        private const int MaxEventsPerHour = 20;
        private const int MaxNumberOfElements = 1000;

        private const string VendorKey = "unity.assetStoreTools";
        private const string EventName = "assetStoreTools";
        
        static bool EnableAnalytics()
        {
            var result = EditorAnalytics.RegisterEventWithLimit(EventName, MaxEventsPerHour, MaxNumberOfElements, VendorKey, VersionId);
            return result == AnalyticsResult.Ok;
        }

        [System.Serializable]
        public struct AnalyticsData
        {
            public string ToolVersion;
            public string PackageId;
            public string Category;
            public bool UsedValidator;
            public string ValidatorResults;
            public string UploadFinishedReason;
            public double TimeTaken;
            public long PackageSize;
            public string Workflow;
            public string EndpointUrl;
        }

        public static void SendUploadingEvent(AnalyticsData data)
        {
            if (!EditorAnalytics.enabled)
                return;

            if (!EnableAnalytics())
                return;
            EditorAnalytics.SendEventWithLimit(EventName, data, VersionId);
        }
    }
}