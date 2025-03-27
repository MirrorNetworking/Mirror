using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models.Results
{
    /// <summary>
    /// Result model for `GET v1/deployments`.
    /// API Doc | https://docs.edgegap.com/api/#tag/Deployments/operation/deployments-get
    /// </summary>
    public class GetDeploymentsResult
    {
        [JsonProperty("data")]
        public GetDeploymentResult[] Data { get; set; }
    }
}
