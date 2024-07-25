using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models.Results
{
    /// <summary>
    /// Result model for `POST v1/deploy`.
    /// </summary>
    public class CreateDeploymentResult
    {
        [JsonProperty("request_id")]
        public string RequestId { get; set; }
        
        [JsonProperty("request_dns")]
        public string RequestDns { get; set; }
        
        [JsonProperty("request_app")]
        public string RequestApp { get; set; }
        
        [JsonProperty("request_version")]
        public string RequestVersion { get; set; }
        
        [JsonProperty("request_user_count")]
        public int RequestUserCount { get; set; }
        
        [JsonProperty("city")]
        public string City { get; set; }
        
        [JsonProperty("country")]
        public string Country { get; set; }
        
        [JsonProperty("continent")]
        public string Continent { get; set; }
        
        [JsonProperty("administrative_division")]
        public string AdministrativeDivision { get; set; }
        
        [JsonProperty("tags")]
        public string[] Tags { get; set; }
        
        [JsonProperty("container_log_storage")]
        public ContainerLogStorageData ContainerLogStorage { get; set; }
        

        public class ContainerLogStorageData
        {
            [JsonProperty("enabled")]
            public bool Enabled { get; set; }
            
            [JsonProperty("endpoint_storage")]
            public string EndpointStorage { get; set; }
        }
    }
}
