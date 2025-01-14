using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models.Results
{
    public class StopActiveDeploymentResult
    {
        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("deployment_summary")]
        public DeploymentSummaryData DeploymentSummary { get; set; }

        public class DeploymentSummaryData
        {
            [JsonProperty("request_id")]
            public string RequestId { get; set; }

            [JsonProperty("fqdn")]
            public string Fqdn { get; set; }

            [JsonProperty("app_name")]
            public string AppName { get; set; }

            [JsonProperty("app_version")]
            public string AppVersion { get; set; }

            [JsonProperty("current_status")]
            public string CurrentStatus { get; set; }

            [JsonProperty("running")]
            public bool Running { get; set; }

            [JsonProperty("whitelisting_active")]
            public bool WhitelistingActive { get; set; }

            [JsonProperty("start_time")]
            public string StartTime { get; set; }

            [JsonProperty("removal_time")]
            public string RemovalTime { get; set; }

            [JsonProperty("elapsed_time")]
            public int? ElapsedTime { get; set; }

            [JsonProperty("last_status")]
            public string LastStatus { get; set; }

            [JsonProperty("error")]
            public bool Error { get; set; }

            [JsonProperty("error_detail")]
            public string ErrorDetail { get; set; }

            [JsonProperty("ports")]
            public PortsData Ports { get; set; }

            [JsonProperty("public_ip")]
            public string PublicIp { get; set; }

            [JsonProperty("sessions")]
            public SessionData[] Sessions { get; set; }

            [JsonProperty("location")]
            public LocationData Location { get; set; }

            [JsonProperty("tags")]
            public string[] Tags { get; set; }

            [JsonProperty("sockets")]
            public string Sockets { get; set; }

            [JsonProperty("sockets_usage")]
            public string SocketsUsage { get; set; }

            [JsonProperty("command")]
            public string Command { get; set; }

            [JsonProperty("arguments")]
            public string Arguments { get; set; }
        }

        public class PortsData { }
    }
}
