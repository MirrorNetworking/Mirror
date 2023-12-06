using System.Net.Http;
using System.Threading.Tasks;
using Edgegap.Editor.Api.Models.Results;
using Newtonsoft.Json.Linq;

namespace Edgegap.Editor.Api
{
    /// <summary>Wraps the v1/wizard API endpoint. Used for internal purposes.</summary>
    public class EdgegapWizardApi : EdgegapApiBase
    {
        /// <summary>Extended path after the base uri</summary>
        public EdgegapWizardApi(
            ApiEnvironment apiEnvironment, 
            string apiToken, 
            EdgegapWindowMetadata.LogLevel logLevel = EdgegapWindowMetadata.LogLevel.Error)
            : base(apiEnvironment, apiToken, logLevel)
        {
        }


        #region API Methods
        /// <summary>POST to v1/wizard/init-quick-start</summary>
        /// <returns>
        /// Http info with no explicit data model
        /// - Success: 204 (no result model)
        /// </returns>
        public async Task<EdgegapHttpResult> InitQuickStart()
        {
            string json = new JObject { ["source"] = "unity" }.ToString();
            HttpResponseMessage response = await PostAsync("v1/wizard/init-quick-start", json);
            EdgegapHttpResult result = new EdgegapHttpResult(response); // MIRROR CHANGE: 'new()' not supported in Unity 2020

            return result;
        }
        
        /// <summary>GET to v1/wizard/registry-credentials</summary>
        /// <returns>
        /// - Http info with GetRegistryCredentialsResult data model
        /// - Success: 200
        /// - Error: Likely if called before a successful InitQuickStart(),
        ///   or if called in a staging env. Soon, this will be available in production.
        /// </returns>
        public async Task<EdgegapHttpResult<GetRegistryCredentialsResult>> GetRegistryCredentials()
        {
            HttpResponseMessage response = await GetAsync("v1/wizard/registry-credentials");
            EdgegapHttpResult<GetRegistryCredentialsResult> result = new EdgegapHttpResult<GetRegistryCredentialsResult>(response); // MIRROR CHANGE: 'new()' not supported in Unity 2020

            return result;
        }
        #endregion // API Methods
    }
}
