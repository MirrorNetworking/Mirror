using AssetStoreTools.Uploader.Data;
using AssetStoreTools.Uploader.Utility;
using AssetStoreTools.Utility;
using AssetStoreTools.Utility.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AssetStoreTools.Uploader
{
    /// <summary>
    /// A class for retrieving data from the Asset Store backend <para/>
    /// <b>Note:</b> most data retrieval methods require <see cref="SavedSessionId"/> to be set
    /// </summary>
    internal static class AssetStoreAPI
    {
        public const string ToolVersion = "V6.3.0";

        private const string UnauthSessionId = "26c4202eb475d02864b40827dfff11a14657aa41";
        private const string KharmaSessionId = "kharma.sessionid";
        private const int UploadResponseTimeoutMs = 10000;

        public static string AssetStoreProdUrl = "https://kharma.unity3d.com";
        private static string s_sessionId = EditorPrefs.GetString(KharmaSessionId);
        private static HttpClient httpClient = new HttpClient();
        private static CancellationTokenSource s_downloadCancellationSource;

        public static string SavedSessionId
        {
            get => s_sessionId;
            set
            {
                s_sessionId = value;
                EditorPrefs.SetString(KharmaSessionId, value);
                httpClient.DefaultRequestHeaders.Clear();
                if (!string.IsNullOrEmpty(value))
                    httpClient.DefaultRequestHeaders.Add("X-Unity-Session", SavedSessionId);
            }
        }

        public static bool IsCloudUserAvailable => CloudProjectSettings.userName != "anonymous";
        public static string LastLoggedInUser = "";
        public static ConcurrentDictionary<string, OngoingUpload> ActiveUploads = new ConcurrentDictionary<string, OngoingUpload>();
        public static bool IsUploading => (ActiveUploads.Count > 0);

        static AssetStoreAPI()
        {
            ServicePointManager.DefaultConnectionLimit = 500;
            httpClient.DefaultRequestHeaders.ConnectionClose = false;
            httpClient.Timeout = TimeSpan.FromMinutes(1320);
        }

        /// <summary>
        /// A structure used to return the success outcome and the result of Asset Store API calls
        /// </summary>
        internal class APIResult
        {
            public JsonValue Response;
            public bool Success;
            public bool SilentFail;
            public ASError Error;

            public static implicit operator bool(APIResult value)
            {
                return value != null && value.Success != false;
            }
        }

        #region Login API

        /// <summary>
        /// A login API call that uses the email and password credentials
        /// </summary>
        /// <remarks>
        /// <b>Note:</b> this method only returns a response from the server and does not set the <see cref="SavedSessionId"/> itself
        /// </remarks>
        public static async Task<APIResult> LoginWithCredentialsAsync(string email, string password)
        {
            FormUrlEncodedContent data = GetLoginContent(new Dictionary<string, string> { { "user", email }, { "pass", password } });
            return await LoginAsync(data);
        }

        /// <summary>
        /// A login API call that uses the <see cref="SavedSessionId"/>
        /// </summary>
        /// <remarks>
        /// <b>Note:</b> this method only returns a response from the server and does not set the <see cref="SavedSessionId"/> itself
        /// </remarks>
        public static async Task<APIResult> LoginWithSessionAsync()
        {
            if (string.IsNullOrEmpty(SavedSessionId))
                return new APIResult() { Success = false, SilentFail = true, Error = ASError.GetGenericError(new Exception("No active session available")) };

            FormUrlEncodedContent data = GetLoginContent(new Dictionary<string, string> { { "reuse_session", SavedSessionId }, { "xunitysession", UnauthSessionId } });
            return await LoginAsync(data);
        }

        /// <summary>
        /// A login API call that uses the <see cref="CloudProjectSettings.accessToken"/><para/>
        /// </summary>
        /// <remarks>
        /// <b>Note:</b> this method only returns a response from the server and does not set the <see cref="SavedSessionId"/> itself
        /// </remarks>
        /// <param name="token">Cloud access token. Can be retrieved by calling <see cref="CloudProjectSettings.accessToken"/></param>
        public static async Task<APIResult> LoginWithTokenAsync(string token)
        {
            FormUrlEncodedContent data = GetLoginContent(new Dictionary<string, string> { { "user_access_token", token } });
            return await LoginAsync(data);
        }

        private static async Task<APIResult> LoginAsync(FormUrlEncodedContent data)
        {
            OverrideAssetStoreUrl();
            Uri uri = new Uri($"{AssetStoreProdUrl}/login");

            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            try
            {
                var response = await httpClient.PostAsync(uri, data);
                return UploadValuesCompletedLogin(response);
            }
            catch (Exception e)
            {
                return new APIResult() { Success = false, Error = ASError.GetGenericError(e) };
            }
        }

        private static APIResult UploadValuesCompletedLogin(HttpResponseMessage response)
        {
            ASDebug.Log($"Upload Values Complete {response.ReasonPhrase}");
            ASDebug.Log($"Login success? {response.IsSuccessStatusCode}");
            try
            {
                response.EnsureSuccessStatusCode();
                var responseResult = response.Content.ReadAsStringAsync().Result;
                var success = JSONParser.AssetStoreResponseParse(responseResult, out ASError error, out JsonValue jsonResult);
                if (success)
                    return new APIResult() { Success = true, Response = jsonResult };
                else
                    return new APIResult() { Success = false, Error = error };
            }
            catch (HttpRequestException ex)
            {
                return new APIResult() { Success = false, Error = ASError.GetLoginError(response, ex) };
            }
        }

        #endregion

        #region Package Metadata API

        private static async Task<JsonValue> GetPackageDataMain()
        {
            return await GetAssetStoreData(APIUri("asset-store-tools", "metadata/0", SavedSessionId));
        }

        private static async Task<JsonValue> GetPackageDataExtra()
        {
            return await GetAssetStoreData(APIUri("management", "packages", SavedSessionId));
        }

        private static async Task<JsonValue> GetCategories(bool useCached)
        {
            if (useCached)
            {
                if (AssetStoreCache.GetCachedCategories(out JsonValue cachedCategoryJson))
                    return cachedCategoryJson;

                ASDebug.LogWarning("Failed to retrieve cached category data. Proceeding to download");
            }
            var categoryJson = await GetAssetStoreData(APIUri("management", "categories", SavedSessionId));
            AssetStoreCache.CacheCategories(categoryJson);

            return categoryJson;
        }

        /// <summary>
        /// Retrieve data for all packages associated with the currently logged in account (identified by <see cref="SavedSessionId"/>)
        /// </summary>
        /// <param name="useCached"></param>
        /// <returns></returns>
        public static async Task<APIResult> GetFullPackageDataAsync(bool useCached)
        {
            if (useCached)
            {
                if (AssetStoreCache.GetCachedPackageMetadata(out JsonValue cachedData))
                    return new APIResult() { Success = true, Response = cachedData };

                ASDebug.LogWarning("Failed to retrieve cached package metadata. Proceeding to download");
            }

            try
            {
                var jsonMainData = await GetPackageDataMain();
                var jsonExtraData = await GetPackageDataExtra();
                var jsonCategoryData = await GetCategories(useCached);

                var joinedData = MergePackageData(jsonMainData, jsonExtraData, jsonCategoryData);
                AssetStoreCache.CachePackageMetadata(joinedData);

                return new APIResult() { Success = true, Response = joinedData };
            }
            catch (OperationCanceledException e)
            {
                ASDebug.Log("Package metadata download operation cancelled");
                DisposeDownloadCancellation();
                return new APIResult() { Success = false, SilentFail = true, Error = ASError.GetGenericError(e) };
            }
            catch (Exception e)
            {
                return new APIResult() { Success = false, Error = ASError.GetGenericError(e) };
            }
        }

        /// <summary>
        /// Retrieve the thumbnail textures for all packages within the provided json structure and perform a given action after each retrieval
        /// </summary>
        /// <param name="packageJson">A json file retrieved from <see cref="GetFullPackageDataAsync(bool)"/></param>
        /// <param name="useCached">Return cached thumbnails if they are found</param>
        /// <param name="onSuccess">
        /// Action to perform upon a successful thumbnail retrieval <para/>
        /// <see cref="string"/> - Package Id <br/>
        /// <see cref="Texture2D"/> - Associated Thumbnail
        /// </param>
        /// <param name="onFail">
        /// Action to perform upon a failed thumbnail retrieval <para/>
        /// <see cref="string"/> - Package Id <br/>
        /// <see cref="ASError"/> - Associated error
        /// </param>
        public static async void GetPackageThumbnails(JsonValue packageJson, bool useCached, Action<string, Texture2D> onSuccess, Action<string, ASError> onFail)
        {
            SetupDownloadCancellation();
            var packageDict = packageJson["packages"].AsDict();
            var packageEnum = packageDict.GetEnumerator();

            for (int i = 0; i < packageDict.Count; i++)
            {
                packageEnum.MoveNext();
                var package = packageEnum.Current;

                try
                {
                    s_downloadCancellationSource.Token.ThrowIfCancellationRequested();

                    if (package.Value["icon_url"]
                        .IsNull()) // If no URL is found in the package metadata, use the default image
                    {
                        Texture2D fallbackTexture = null;
                        ASDebug.Log($"Package {package.Key} has no thumbnail. Returning default image");
                        onSuccess?.Invoke(package.Key, fallbackTexture);
                        continue;
                    }

                    if (useCached &&
                        AssetStoreCache.GetCachedTexture(package.Key,
                            out Texture2D texture)) // Try returning cached thumbnails first 
                    {
                        ASDebug.Log($"Returning cached thumbnail for package {package.Key}");
                        onSuccess?.Invoke(package.Key, texture);
                        continue;
                    }

                    var textureBytes =
                        await DownloadPackageThumbnail(package.Value["icon_url"].AsString());
                    Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    tex.LoadImage(textureBytes);
                    AssetStoreCache.CacheTexture(package.Key, tex);
                    ASDebug.Log($"Returning downloaded thumbnail for package {package.Key}");
                    onSuccess?.Invoke(package.Key, tex);
                }
                catch (OperationCanceledException)
                {
                    DisposeDownloadCancellation();
                    ASDebug.Log("Package thumbnail download operation cancelled");
                    return;
                }
                catch (Exception e)
                {
                    onFail?.Invoke(package.Key, ASError.GetGenericError(e));
                }
                finally
                {
                    packageEnum.Dispose();
                }
            }
        }

        private static async Task<byte[]> DownloadPackageThumbnail(string url)
        {
            // icon_url is presented without http/https
            Uri uri = new Uri($"https:{url}");

            var textureBytes = await httpClient.GetAsync(uri, s_downloadCancellationSource.Token).
                ContinueWith((response) => response.Result.Content.ReadAsByteArrayAsync().Result, s_downloadCancellationSource.Token);
            s_downloadCancellationSource.Token.ThrowIfCancellationRequested();
            return textureBytes;
        }

        /// <summary>
        /// Retrieve, update the cache and return the updated data for a previously cached package
        /// </summary>
        public static async Task<APIResult> GetRefreshedPackageData(string packageId)
        {
            try
            {
                var refreshedDataJson = await GetPackageDataExtra();
                var refreshedPackage = default(JsonValue);

                // Find the updated package data in the latest data json
                foreach (var p in refreshedDataJson["packages"].AsList())
                {
                    if (p["id"] == packageId)
                    {
                        refreshedPackage = p["versions"].AsList()[p["versions"].AsList().Count - 1];
                        break;
                    }
                }

                if (refreshedPackage.Equals(default(JsonValue)))
                    return new APIResult() { Success = false, Error = ASError.GetGenericError(new MissingMemberException($"Unable to find downloaded package data for package id {packageId}")) };

                // Check if the supplied package id data has been cached and if it contains the corresponding package
                if (!AssetStoreCache.GetCachedPackageMetadata(out JsonValue cachedData) ||
                    !cachedData["packages"].AsDict().ContainsKey(packageId))
                    return new APIResult() { Success = false, Error = ASError.GetGenericError(new MissingMemberException($"Unable to find cached package id {packageId}")) };

                var cachedPackage = cachedData["packages"].AsDict()[packageId];

                // Retrieve the category map
                var categoryJson = await GetCategories(true);
                var categories = CreateCategoryDictionary(categoryJson);

                // Update the package data
                cachedPackage["name"] = refreshedPackage["name"].AsString();
                cachedPackage["status"] = refreshedPackage["status"].AsString();
                cachedPackage["extra_info"].AsDict()["category_info"].AsDict()["id"] = refreshedPackage["category_id"].AsString();
                cachedPackage["extra_info"].AsDict()["category_info"].AsDict()["name"] =
                    categories.ContainsKey(refreshedPackage["category_id"]) ? categories[refreshedPackage["category_id"].AsString()] : "Unknown";
                cachedPackage["extra_info"].AsDict()["modified"] = refreshedPackage["modified"].AsString();
                cachedPackage["extra_info"].AsDict()["size"] = refreshedPackage["size"].AsString();

                AssetStoreCache.CachePackageMetadata(cachedData);
                return new APIResult() { Success = true, Response = cachedPackage };
            }
            catch (OperationCanceledException)
            {
                ASDebug.Log("Package metadata download operation cancelled");
                DisposeDownloadCancellation();
                return new APIResult() { Success = false, SilentFail = true };
            }
            catch (Exception e)
            {
                return new APIResult() { Success = false, Error = ASError.GetGenericError(e) };
            }
        }

        /// <summary>
        /// Retrieve all Unity versions that the given package has already had uploaded content with
        /// </summary>
        /// <param name="packageId"></param>
        /// <param name="versionId"></param>
        /// <returns></returns>
        public static List<string> GetPackageUploadedVersions(string packageId, string versionId)
        {
            var versions = new List<string>();
            try
            {
                // Retrieve the data for already uploaded versions (should prevent interaction with Uploader)
                var versionsTask = Task.Run(() => GetAssetStoreData(APIUri("content", $"preview/{packageId}/{versionId}", SavedSessionId)));
                if (!versionsTask.Wait(5000))
                    throw new TimeoutException("Could not retrieve uploaded versions within a reasonable time interval");

                var versionsJson = versionsTask.Result;
                foreach (var version in versionsJson["content"].AsDict()["unity_versions"].AsList())
                    versions.Add(version.AsString());
            }
            catch (OperationCanceledException)
            {
                ASDebug.Log("Package version download operation cancelled");
                DisposeDownloadCancellation();
            }
            catch (Exception e)
            {
                ASDebug.LogError(e);
            }

            return versions;
        }

        #endregion

        #region Package Upload API

        /// <summary>
        /// Upload a content file (.unitypackage) to a provided package version
        /// </summary>
        /// <param name="versionId"></param>
        /// <param name="packageName">Name of the package. Only used for identifying the package in <see cref="OngoingUpload"/> class</param>
        /// <param name="filePath">Path to the .unitypackage file</param>
        /// <param name="localPackageGuid">The <see cref="AssetDatabase.AssetPathToGUID(string)"/> value of the main content folder for the provided package</param>
        /// <param name="localPackagePath">The local path (relative to the root project folder) of the main content folder for the provided package</param>
        /// <param name="localProjectPath">The path to the project that this package was built from</param>
        /// <returns></returns>
        public static async Task<PackageUploadResult> UploadPackageAsync(string versionId, string packageName, string filePath,
            string localPackageGuid, string localPackagePath, string localProjectPath)
        {
            try
            {
                ASDebug.Log("Upload task starting");
                EditorApplication.LockReloadAssemblies();

                if (!IsUploading) // Only subscribe before the first upload
                    EditorApplication.playModeStateChanged += EditorPlayModeStateChangeHandler;

                var progressData = new OngoingUpload(versionId, packageName);
                ActiveUploads.TryAdd(versionId, progressData);

                var result = await Task.Run(() => UploadPackageTask(progressData, filePath, localPackageGuid, localPackagePath, localProjectPath));

                ActiveUploads.TryRemove(versionId, out OngoingUpload _);

                ASDebug.Log("Upload task finished");
                return result;
            }
            catch (Exception e)
            {
                ASDebug.LogError("Upload task failed with an exception: " + e);
                ActiveUploads.TryRemove(versionId, out OngoingUpload _);
                return PackageUploadResult.PackageUploadFail(ASError.GetGenericError(e));
            }
            finally
            {
                if (!IsUploading) // Only unsubscribe after the last upload
                    EditorApplication.playModeStateChanged -= EditorPlayModeStateChangeHandler;

                EditorApplication.UnlockReloadAssemblies();
            }
        }

        private static PackageUploadResult UploadPackageTask(OngoingUpload currentUpload, string filePath,
            string localPackageGuid, string localPackagePath, string localProjectPath)
        {
            ASDebug.Log("Preparing to upload package within API");
            string api = "asset-store-tools";
            string uri = $"package/{currentUpload.VersionId}/unitypackage";

            Dictionary<string, string> packageParams = new Dictionary<string, string>
            {
                // Note: project_path is currently used to store UI selections
                {"root_guid", localPackageGuid},
                {"root_path", localPackagePath},
                {"project_path", localProjectPath}
            };

            ASDebug.Log($"Creating upload request for {currentUpload.VersionId} {currentUpload.PackageName}");

            FileStream requestFileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

            bool responseTimedOut = false;
            long chunkSize = 32768;
            try
            {
                ASDebug.Log("Starting upload process...");

                var content = new StreamContent(requestFileStream, (int)chunkSize);
                var response = httpClient.PutAsync(APIUri(api, uri, SavedSessionId, packageParams), content, currentUpload.CancellationToken);

                // Progress tracking
                int updateIntervalMs = 100;
                bool allBytesSent = false;
                DateTime timeOfCompletion = default(DateTime);

                while (!response.IsCompleted)
                {
                    float uploadProgress = (float)requestFileStream.Position / requestFileStream.Length * 100;
                    currentUpload.UpdateProgress(uploadProgress);
                    Thread.Sleep(updateIntervalMs);

                    // A timeout for rare cases, when package uploading reaches 100%, but PutAsync task IsComplete value remains 'False'
                    if (requestFileStream.Position == requestFileStream.Length)
                    {
                        if (!allBytesSent)
                        {
                            allBytesSent = true;
                            timeOfCompletion = DateTime.UtcNow;
                        }
                        else if (DateTime.UtcNow.Subtract(timeOfCompletion).TotalMilliseconds > UploadResponseTimeoutMs)
                        {
                            responseTimedOut = true;
                            currentUpload.Cancel();
                            break;
                        }
                    }
                }

                // 2020.3 - although cancellation token shows a requested cancellation, the HttpClient
                // tends to return a false 'IsCanceled' value, thus yielding an exception when attempting to read the response.
                // For now we'll just check the token as well, but this needs to be investigated later on.
                if (response.IsCanceled || currentUpload.CancellationToken.IsCancellationRequested)
                    currentUpload.CancellationToken.ThrowIfCancellationRequested();

                var responseString = response.Result.Content.ReadAsStringAsync().Result;

                var success = JSONParser.AssetStoreResponseParse(responseString, out ASError error, out JsonValue json);
                ASDebug.Log("Upload response JSON: " + json.ToString());
                if (success)
                    return PackageUploadResult.PackageUploadSuccess();
                else
                    return PackageUploadResult.PackageUploadFail(error);
            }
            catch (OperationCanceledException)
            {
                // Uploading is canceled
                if (!responseTimedOut)
                {
                    ASDebug.Log("Upload operation cancelled");
                    return PackageUploadResult.PackageUploadCancelled();
                }
                else
                {
                    ASDebug.LogWarning("All data has been uploaded, but waiting for the response timed out");
                    return PackageUploadResult.PackageUploadResponseTimeout();
                }
            }
            catch (Exception e)
            {
                ASDebug.LogError("Upload operation encountered an undefined exception: " + e);
                var fullError = e.InnerException != null ? ASError.GetGenericError(e.InnerException) : ASError.GetGenericError(e);
                return PackageUploadResult.PackageUploadFail(fullError);
            }
            finally
            {
                requestFileStream.Dispose();
                currentUpload.Dispose();
            }
        }

        /// <summary>
        /// Cancel the uploading task for a package with the provided package id
        /// </summary>
        public static void AbortPackageUpload(string packageId)
        {
            ActiveUploads[packageId]?.Cancel();
        }

        #endregion

        #region Utility Methods
        private static string GetLicenseHash()
        {
            return UnityEditorInternal.InternalEditorUtility.GetAuthToken().Substring(0, 40);
        }

        private static string GetHardwareHash()
        {
            return UnityEditorInternal.InternalEditorUtility.GetAuthToken().Substring(40, 40);
        }

        private static FormUrlEncodedContent GetLoginContent(Dictionary<string, string> loginData)
        {
            loginData.Add("unityversion", Application.unityVersion);
            loginData.Add("toolversion", ToolVersion);
            loginData.Add("license_hash", GetLicenseHash());
            loginData.Add("hardware_hash", GetHardwareHash());

            return new FormUrlEncodedContent(loginData);
        }

        private static async Task<JsonValue> GetAssetStoreData(Uri uri)
        {
            SetupDownloadCancellation();

            var response = await httpClient.GetAsync(uri, s_downloadCancellationSource.Token)
                .ContinueWith((x) => x.Result.Content.ReadAsStringAsync().Result, s_downloadCancellationSource.Token);
            s_downloadCancellationSource.Token.ThrowIfCancellationRequested();

            if (!JSONParser.AssetStoreResponseParse(response, out var error, out var jsonMainData))
                throw error.Exception;

            return jsonMainData;
        }

        private static Uri APIUri(string apiPath, string endPointPath, string sessionId)
        {
            return APIUri(apiPath, endPointPath, sessionId, null);
        }

        // Method borrowed from A$ tools, could maybe be simplified to only retain what is necessary?
        private static Uri APIUri(string apiPath, string endPointPath, string sessionId, IDictionary<string, string> extraQuery)
        {
            Dictionary<string, string> extraQueryMerged;

            if (extraQuery == null)
                extraQueryMerged = new Dictionary<string, string>();
            else
                extraQueryMerged = new Dictionary<string, string>(extraQuery);

            extraQueryMerged.Add("unityversion", Application.unityVersion);
            extraQueryMerged.Add("toolversion", ToolVersion);
            extraQueryMerged.Add("xunitysession", sessionId);

            string uriPath = $"{AssetStoreProdUrl}/api/{apiPath}/{endPointPath}.json";
            UriBuilder uriBuilder = new UriBuilder(uriPath);

            StringBuilder queryToAppend = new StringBuilder();
            foreach (KeyValuePair<string, string> queryPair in extraQueryMerged)
            {
                string queryName = queryPair.Key;
                string queryValue = Uri.EscapeDataString(queryPair.Value);

                queryToAppend.AppendFormat("&{0}={1}", queryName, queryValue);
            }
            if (!string.IsNullOrEmpty(uriBuilder.Query))
                uriBuilder.Query = uriBuilder.Query.Substring(1) + queryToAppend;
            else
                uriBuilder.Query = queryToAppend.Remove(0, 1).ToString();

            return uriBuilder.Uri;
        }

        private static JsonValue MergePackageData(JsonValue mainPackageData, JsonValue extraPackageData, JsonValue categoryData)
        {
            ASDebug.Log($"Main package data\n{mainPackageData}");
            var mainDataDict = mainPackageData["packages"].AsDict();

            // Most likely both of them will be true at the same time, but better to be safe
            if (mainDataDict.Count == 0 || !extraPackageData.ContainsKey("packages"))
                return new JsonValue();

            ASDebug.Log($"Extra package data\n{extraPackageData}");
            var extraDataDict = extraPackageData["packages"].AsList();

            var categories = CreateCategoryDictionary(categoryData);

            foreach (var md in mainDataDict)
            {
                foreach (var ed in extraDataDict)
                {
                    if (ed["id"].AsString() != md.Key)
                        continue;

                    // Create a field for extra data
                    var extraData = JsonValue.NewDict();

                    // Add category field
                    var categoryEntry = JsonValue.NewDict();

                    var categoryId = ed["category_id"].AsString();
                    var categoryName = categories.ContainsKey(categoryId) ? categories[categoryId] : "Unknown";

                    categoryEntry["id"] = categoryId;
                    categoryEntry["name"] = categoryName;

                    extraData["category_info"] = categoryEntry;

                    // Add modified time and size
                    var versions = ed["versions"].AsList();
                    extraData["modified"] = versions[versions.Count - 1]["modified"];
                    extraData["size"] = versions[versions.Count - 1]["size"];

                    md.Value.AsDict()["extra_info"] = extraData;
                }
            }

            mainPackageData.AsDict()["packages"] = new JsonValue(mainDataDict);
            return mainPackageData;
        }

        private static Dictionary<string, string> CreateCategoryDictionary(JsonValue json)
        {
            var categories = new Dictionary<string, string>();

            var list = json.AsList();

            for (int i = 0; i < list.Count; i++)
            {
                var category = list[i].AsDict();
                if (category["status"].AsString() == "deprecated")
                    continue;
                categories.Add(category["id"].AsString(), category["assetstore_name"].AsString());
            }

            return categories;
        }

        /// <summary>
        /// Check if the account data is for a valid publisher account
        /// </summary>
        /// <param name="json">Json structure retrieved from one of the API login methods</param>
        public static bool IsPublisherValid(JsonValue json, out ASError error)
        {
            error = ASError.GetPublisherNullError(json["name"]);

            if (!json.ContainsKey("publisher"))
                return false;

            // If publisher account is not created - let them know
            return !json["publisher"].IsNull();
        }

        /// <summary>
        /// Cancel all data retrieval tasks
        /// </summary>
        public static void AbortDownloadTasks()
        {
            s_downloadCancellationSource?.Cancel();
        }

        /// <summary>
        /// Cancel all data uploading tasks
        /// </summary>
        public static void AbortUploadTasks()
        {
            foreach (var upload in ActiveUploads)
            {
                AbortPackageUpload(upload.Key);
            }
        }

        private static void SetupDownloadCancellation()
        {
            if (s_downloadCancellationSource != null && s_downloadCancellationSource.IsCancellationRequested)
                DisposeDownloadCancellation();

            if (s_downloadCancellationSource == null)
                s_downloadCancellationSource = new CancellationTokenSource();
        }

        private static void DisposeDownloadCancellation()
        {
            s_downloadCancellationSource?.Dispose();
            s_downloadCancellationSource = null;
        }

        private static void EditorPlayModeStateChangeHandler(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
                return;

            EditorApplication.ExitPlaymode();
            EditorUtility.DisplayDialog("Notice", "Entering Play Mode is not allowed while there's a package upload in progress.\n\n" +
                                                  "Please wait until the upload is finished or cancel the upload from the Asset Store Uploader window", "OK");
        }

        private static void OverrideAssetStoreUrl()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (!args[i].Equals("-assetStoreUrl"))
                    continue;

                if (i + 1 >= args.Length)
                    return;

                ASDebug.Log($"Overriding A$ URL to: {args[i + 1]}");
                AssetStoreProdUrl = args[i + 1];
                return;
            }
        }

        #endregion
    }
}