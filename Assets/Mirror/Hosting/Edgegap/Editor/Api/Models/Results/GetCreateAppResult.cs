using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models.Results
{
    /// <summary>
    /// Result model for `[GET | POST] v1/app`.
    /// POST API Doc | https://docs.edgegap.com/api/#tag/Applications/operation/application-post
    /// GET API Doc | https://docs.edgegap.com/api/#tag/Applications/operation/application-get
    /// </summary>
    public class GetCreateAppResult
    {
        [JsonProperty("name")]
        public string AppName { get; set; }
        
        [JsonProperty("is_active")]
        public bool IsActive { get; set; }
        
        /// <summary>Optional</summary>
        [JsonProperty("is_telemetry_agent_active")]
        public bool IsTelemetryAgentActive { get; set; }
        
        [JsonProperty("image")]
        public string Image { get; set; }
        
        [JsonProperty("create_time")]
        public string CreateTimeStr { get; set; }
        
        [JsonProperty("last_updated")]
        public string LastUpdatedStr { get; set; }
    }
}
