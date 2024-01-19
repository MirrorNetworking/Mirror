using System.Collections.Generic;
using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models.Results
{
    /// <summary>
    /// Result model for `GET v1/status/{request_id}`.
    /// API Doc | https://docs.edgegap.com/api/#tag/Deployments/operation/deployment-status-get
    /// </summary>
    public class GetDeploymentStatusResult
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
        
        /// <summary>
        /// TODO: Server should swap `ports` to an array of DeploymentPortsData (instead of an object of dynamic unknown objects).
        /// <example>
        /// {
        ///     "7777", {}
        /// },
        /// {
        ///     "Some Port Name", {}
        /// }
        /// </example>
        /// </summary>
        [JsonProperty("ports")]
        public Dictionary<string, DeploymentPortsData> PortsDict { get; set; }
    }
}
