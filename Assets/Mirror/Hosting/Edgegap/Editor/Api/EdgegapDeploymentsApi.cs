using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Edgegap.Editor.Api.Models.Requests;
using Edgegap.Editor.Api.Models.Results;
using UnityEngine.Assertions;

namespace Edgegap.Editor.Api
{
    /// <summary>
    /// Wraps the v1/[deploy | status | stop] API endpoints: Deployments Control API.
    /// - API Doc | https://docs.edgegap.com/api/#tag/Deployments 
    /// </summary>
    public class EdgegapDeploymentsApi : EdgegapApiBase
    {
        public EdgegapDeploymentsApi(
            ApiEnvironment apiEnvironment, 
            string apiToken, 
            EdgegapWindowMetadata.LogLevel logLevel = EdgegapWindowMetadata.LogLevel.Error)
            : base(apiEnvironment, apiToken, logLevel)
        {
        }


        #region API Methods
        /// <summary>
        /// POST v1/deploy
        /// - Create a new deployment. Deployment is a server instance of your application version.
        /// - API Doc | https://docs.edgegap.com/api/#tag/Deployments
        /// </summary>
        /// <returns>
        /// Http info with CreateDeploymentResult data model
        /// - Success: 200
        /// </returns>
        public async Task<EdgegapHttpResult<CreateDeploymentResult>> CreateDeploymentAsync(
            CreateDeploymentRequest request)
        {
            HttpResponseMessage response = await PostAsync("v1/deploy", request.ToString());
            EdgegapHttpResult<CreateDeploymentResult> result = new EdgegapHttpResult<CreateDeploymentResult>(response); // MIRROR CHANGE: 'new()' not supported in Unity 2020

            bool isSuccess = response.StatusCode == HttpStatusCode.OK; // 200
            if (!isSuccess)
                return result;
            
            return result;
        }
        
        /// <summary>
        /// GET v1/status/{requestId}
        /// - Retrieve the information for a deployment.
        /// - API Doc | https://docs.edgegap.com/api/#tag/Deployments/operation/deployment-status-get
        /// </summary>
        /// <param name="requestId">
        /// Unique Identifier to keep track of your request across all Arbitrium ecosystem.
        /// It's included in the response of the app deploy. Ex: "93924761ccde"</param>
        /// <returns>
        /// Http info with GetDeploymentStatusResult data model
        /// - Success: 200
        /// </returns>
        public async Task<EdgegapHttpResult<GetDeploymentStatusResult>> GetDeploymentStatusAsync(string requestId)
        {
            HttpResponseMessage response = await GetAsync($"v1/status/{requestId}");
            EdgegapHttpResult<GetDeploymentStatusResult> result = new EdgegapHttpResult<GetDeploymentStatusResult>(response); // MIRROR CHANGE: 'new()' not supported in Unity 2020

            bool isSuccess = response.StatusCode == HttpStatusCode.OK; // 200
            if (!isSuccess)
                return result;
            
            return result;
        }
        
        /// <summary>
        /// DELETE v1/stop/{requestId}
        /// - Delete an instance of deployment. It will stop the running container and all its games.
        /// - API Doc | https://docs.edgegap.com/api/#tag/Deployments/operation/deployment-status-get
        /// </summary>
        /// <param name="requestId">
        /// Unique Identifier to keep track of your request across all Arbitrium ecosystem.
        /// It's included in the response of the app deploy. Ex: "93924761ccde"</param>
        /// <returns>
        /// Http info with GetDeploymentStatusResult data model
        /// - Success: 200
        /// </returns>
        public async Task<EdgegapHttpResult<StopActiveDeploymentResult>> StopActiveDeploymentAsync(string requestId)
        {
            HttpResponseMessage response = await DeleteAsync($"v1/stop/{requestId}");
            EdgegapHttpResult<StopActiveDeploymentResult> result = new EdgegapHttpResult<StopActiveDeploymentResult>(response); // MIRROR CHANGE: 'new()' not supported in Unity 2020

            bool isSuccess = response.StatusCode == HttpStatusCode.OK; // 200
            if (!isSuccess)
                return result;
            
            return result;
        }
        #endregion // API Methods
        
        
        #region Chained API Methods
        /// <summary>
        /// POST v1/deploy => GET v1/status/{requestId}
        /// - Create a new deployment. Deployment is a server instance of your application version.
        /// - Then => await READY status.
        /// - API Doc | https://docs.edgegap.com/api/#tag/Deployments
        /// </summary>
        /// <returns>
        /// Http info with CreateDeploymentResult data model (with a READY deployment status)
        /// - Success: 200
        /// - Error: If createResult.HasErr, returns createResult
        /// </returns>
        public async Task<EdgegapHttpResult<CreateDeploymentResult>> CreateDeploymentAwaitReadyStatusAsync(
            CreateDeploymentRequest request, TimeSpan pollInterval)
        {
            EdgegapHttpResult<CreateDeploymentResult> createResponse = await CreateDeploymentAsync(request);

            // Create =>
            bool isCreateSuccess = createResponse.StatusCode == HttpStatusCode.OK; // 200
            if (!isCreateSuccess)
                return createResponse;
            
            // Await Status READY =>
            string requestId = createResponse.Data.RequestId;
            _ = await AwaitReadyStatusAsync(requestId, pollInterval);

            // Return no matter what the result; no need to validate
            return createResponse;
        }
        
        /// <summary>If you recently deployed but want to await READY status.</summary>
        /// <param name="requestId"></param>
        /// <param name="pollInterval"></param>
        public async Task<EdgegapHttpResult<GetDeploymentStatusResult>> AwaitReadyStatusAsync(
            string requestId, 
            TimeSpan pollInterval)
        {
            Assert.IsTrue(!string.IsNullOrEmpty(requestId)); // Validate
            
            EdgegapHttpResult<GetDeploymentStatusResult> statusResponse = null;
            CancellationTokenSource cts = new CancellationTokenSource (TimeSpan.FromMinutes( // MIRROR CHANGE: 'new()' not supported in Unity 2020
                EdgegapWindowMetadata.DEPLOYMENT_AWAIT_READY_STATUS_TIMEOUT_MINS));
            bool isReady = false;
            
            while (!isReady && !cts.Token.IsCancellationRequested)
            {
                await Task.Delay(pollInterval, cts.Token);
                statusResponse = await GetDeploymentStatusAsync(requestId);
                isReady = statusResponse.Data.CurrentStatus == EdgegapWindowMetadata.READY_STATUS;
            }

            return statusResponse;
        }
        
        /// <summary>If you recently stopped a deployment, but want to await TERMINATED (410) status.</summary>
        /// <param name="requestId"></param>
        /// <param name="pollInterval"></param>
        public async Task<EdgegapHttpResult<StopActiveDeploymentResult>> AwaitTerminatedDeleteStatusAsync(
            string requestId, 
            TimeSpan pollInterval)
        {
            EdgegapHttpResult<StopActiveDeploymentResult> deleteResponse = null;
            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMinutes( // MIRROR CHANGE: 'new()' not supported in Unity 2020
                EdgegapWindowMetadata.DEPLOYMENT_AWAIT_READY_STATUS_TIMEOUT_MINS));
            bool isStopped = false;
            
            while (!isStopped && !cts.Token.IsCancellationRequested)
            {
                await Task.Delay(pollInterval, cts.Token);
                deleteResponse = await StopActiveDeploymentAsync(requestId);
                isStopped = deleteResponse.StatusCode == HttpStatusCode.Gone; // 410
            }

            return deleteResponse;
        }
        #endregion Chained API Methods
    }
}
