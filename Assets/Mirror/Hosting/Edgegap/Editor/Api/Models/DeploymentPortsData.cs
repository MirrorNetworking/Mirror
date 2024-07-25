using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models
{
    /// <summary>Used in `GetDeploymentStatus`.</summary>
    public class DeploymentPortsData
    {
        [JsonProperty("external")]
        public int External { get; set; }
        
        [JsonProperty("internal")]
        public int Internal { get; set; }
        
        [JsonProperty("protocol")]
        public string Protocol { get; set; }
        
        [JsonProperty("name")]
        public string PortName { get; set; }
        
        [JsonProperty("tls_upgrade")]
        public bool TlsUpgrade { get; set; }
        
        [JsonProperty("link")]
        public string Link { get; set; }
        
        [JsonProperty("proxy")]
        public int? Proxy { get; set; }
    }
}
