using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Edgegap.Editor.Api.Models.Results;

namespace Edgegap.Editor.Api
{
    /// <summary>
    /// Wraps the v1/ip API endpoint: "IP Lookup" API.
    /// - API Doc | https://docs.edgegap.com/api/#tag/IP-Lookup
    /// </summary>
    public class EdgegapIpApi : EdgegapApiBase
    {
        public EdgegapIpApi(
            ApiEnvironment apiEnvironment,
            string apiToken,
            EdgegapWindowMetadata.LogLevel logLevel = EdgegapWindowMetadata.LogLevel.Error
        )
            : base(apiEnvironment, apiToken, logLevel) { }

        #region API Methods
        /// <summary>
        /// GET to v1/app
        /// - Retrieve your public IP address.
        /// - API Doc | https://docs.edgegap.com/api/#tag/IP-Lookup/operation/IP
        /// </summary>
        /// <returns>
        /// Http info with GetCreateAppResult data model
        /// - Success: 200
        /// - Fail: 409 (app already exists), 400 (reached limit)
        /// </returns>
        public async Task<EdgegapHttpResult<GetYourPublicIpResult>> GetYourPublicIp()
        {
            HttpResponseMessage response = await GetAsync("v1/ip");
            EdgegapHttpResult<GetYourPublicIpResult> result =
                new EdgegapHttpResult<GetYourPublicIpResult>(response); // MIRROR CHANGE: 'new()' not supported in Unity 2020

            return result;
        }
        #endregion // API Methods
    }
}
