using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models.Results
{
    /// <summary>
    /// Result model of a deployment for `GET v1/deployments`.
    /// API Doc | https://docs.edgegap.com/api/#tag/Deployments/operation/deployments-get
    /// </summary>
    public class GetDeploymentResult
    {
        [JsonProperty("request_id")]
        public string RequestId { get; set; }

        [JsonProperty("ready")]
        public bool Ready { get; set; }

        [JsonProperty("tags")]
        public string[] Tags { get; set; }
    }
}
