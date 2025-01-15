using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Edgegap.Editor.Api.Models.Requests;
using Edgegap.Editor.Api.Models.Results;

namespace Edgegap.Editor.Api
{
    /// <summary>
    /// Wraps the v1/app API endpoint: Applications Control API.
    /// - API Doc | https://docs.edgegap.com/api/#tag/Applications 
    /// </summary>
    public class EdgegapAppApi : EdgegapApiBase
    {
        public EdgegapAppApi(
            ApiEnvironment apiEnvironment, 
            string apiToken, 
            EdgegapWindowMetadata.LogLevel logLevel = EdgegapWindowMetadata.LogLevel.Error)
            : base(apiEnvironment, apiToken, logLevel)
        {
        }


        #region API Methods
        /// <summary>
        /// POST to v1/app
        /// - Create an application that will regroup application versions.
        /// - API Doc | https://docs.edgegap.com/api/#tag/Applications/operation/application-post 
        /// </summary>
        /// <returns>
        /// Http info with GetCreateAppResult data model
        /// - Success: 200
        /// - Fail: 409 (app already exists), 400 (reached limit)
        /// </returns>
        public async Task<EdgegapHttpResult<GetCreateAppResult>> CreateApp(CreateAppRequest request)
        {
            HttpResponseMessage response = await PostAsync("v1/app", request.ToString());
            EdgegapHttpResult<GetCreateAppResult> result = new EdgegapHttpResult<GetCreateAppResult>(response); // MIRROR CHANGE: 'new()' not supported in Unity 2020

            bool isSuccess = response.StatusCode == HttpStatusCode.OK; // 200
            if (!isSuccess)
                return result;
            
            return result;
        }
        
        /// <summary>
        /// GET to v1/app
        /// - Get an application that will regroup application versions.
        /// - API Doc | https://docs.edgegap.com/api/#tag/Applications/operation/application-post 
        /// </summary>
        /// <returns>
        /// Http info with GetCreateAppResult data model
        /// - Success: 200
        /// </returns>
        public async Task<EdgegapHttpResult<GetCreateAppResult>> GetApp(string appName)
        {
            HttpResponseMessage response = await GetAsync($"v1/app/{appName}");
            EdgegapHttpResult<GetCreateAppResult> result = new EdgegapHttpResult<GetCreateAppResult>(response); // MIRROR CHANGE: 'new()' not supported in Unity 2020

            bool isSuccess = response.StatusCode == HttpStatusCode.OK; // 200
            if (!isSuccess)
                return result;
            
            return result;
        }
        
        /// <summary>
        /// PATCH to v1/app/{app_name}/version/{version_name}
        /// - Update an *existing* application version with new specifications.
        /// - API Doc | https://docs.edgegap.com/api/#tag/Applications/operation/app-versions-patch
        /// </summary>
        /// <returns>
        /// Http info with UpdateAppVersionRequest data model
        /// - Success: 200
        /// </returns>
        public async Task<EdgegapHttpResult<UpsertAppVersionResult>> UpdateAppVersion(UpdateAppVersionRequest request)
        {
            string relativePath = $"v1/app/{request.AppName}/version/{request.VersionName}";
            HttpResponseMessage response = await PatchAsync(relativePath, request.ToString());
            EdgegapHttpResult<UpsertAppVersionResult> result = new EdgegapHttpResult<UpsertAppVersionResult>(response); // MIRROR CHANGE: 'new()' not supported in Unity 2020

            bool isSuccess = response.StatusCode == HttpStatusCode.OK; // 200
            if (!isSuccess)
                return result;
            
            return result;
        }

        /// <summary>
        /// POST to v1/app/{app_name}/version
        /// - Create an new application version with new specifications.
        /// - API Doc | https://docs.edgegap.com/api/#tag/Applications/operation/app-version-post
        /// </summary>
        /// <returns>
        /// Http info with UpdateAppVersionRequest data model
        /// - Success: 200 (no result model)
        /// - Fail: 409 (app already exists), 400 (reached limit)
        /// </returns>
        public async Task<EdgegapHttpResult<UpsertAppVersionResult>> CreateAppVersion(CreateAppVersionRequest request)
        {
            string relativePath = $"v1/app/{request.AppName}/version";
            HttpResponseMessage response = await PostAsync(relativePath, request.ToString());
            EdgegapHttpResult<UpsertAppVersionResult> result = new EdgegapHttpResult<UpsertAppVersionResult>(response); // MIRROR CHANGE: 'new()' not supported in Unity 2020

            bool isSuccess = response.StatusCode == HttpStatusCode.OK; // 200

            if (!isSuccess)
                return result;

            return result;
        }
        #endregion // API Methods
        
        
        #region Chained API Methods
        /// <summary>
        /// PATCH and/or POST to v1/app/: Upsert an *existing* application version with new specifications.
        /// - Consumes either 1 or 2 API calls: 1st tries to PATCH, then POST if PATCH fails (!exists).
        /// - API POST Doc | https://docs.edgegap.com/api/#tag/Applications/operation/app-version-post
        /// - API PATCH Doc | https://docs.edgegap.com/api/#tag/Applications/operation/app-versions-patch
        /// </summary>
        /// <returns>
        /// Http info with UpdateAppVersionRequest data model
        /// - Success: 200 (no result model)
        /// - Fail: 409 (app already exists), 400 (reached limit)
        /// </returns>
        public async Task<EdgegapHttpResult<UpsertAppVersionResult>> UpsertAppVersion(UpdateAppVersionRequest request)
        {
            EdgegapHttpResult<UpsertAppVersionResult> result = await UpdateAppVersion(request); // PATCH

            if (result.HasErr)
            {
                // Try to create, instead
                CreateAppVersionRequest createAppVersionRequest = CreateAppVersionRequest.FromUpdateRequest(request);
                result = await CreateAppVersion(createAppVersionRequest); // POST
            }
            
            bool isSuccess = result.StatusCode == HttpStatusCode.OK; // 200

            if (!isSuccess)
                return result;

            return result;
        }
        #endregion // Chained API Methods 
    }
}
