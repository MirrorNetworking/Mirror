using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models.Results
{
    /// <summary>
    /// Result model for `GET v1/ip`.
    /// GET API Doc | https://docs.edgegap.com/api/#tag/IP-Lookup/operation/IP
    /// </summary>
    public class GetYourPublicIpResult
    {
        [JsonProperty("public_ip")]
        public string PublicIp { get; set; }
    }
}
