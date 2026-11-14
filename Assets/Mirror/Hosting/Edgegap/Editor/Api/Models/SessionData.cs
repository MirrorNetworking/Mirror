using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models
{
    /// <summary>
    /// Shared model for `GetDeploymentStatusResult`, `StopActiveDeploymentResult`.
    /// </summary>
    public class SessionData
    {
        [JsonProperty("session_id")]
        public string SessionId { get; set; }
            
        [JsonProperty("status")]
        public string Status { get; set; }
            
        [JsonProperty("ready")]
        public bool Ready { get; set; }
            
        [JsonProperty("linked")]
        public bool Linked { get; set; }
            
        [JsonProperty("kind")]
        public string Kind { get; set; }
            
        [JsonProperty("user_count")]
        public string UserCount { get; set; }
    }
}
