using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models
{
    /// <summary>
    /// Used in `UpdateAppVersionRequest`, `CreateAppVersionRequest`.
    /// For GetDeploymentStatusResult, see DeploymentPortsData
    /// </summary>
    public class AppPortsData
    {
        /// <summary>1024~49151; Default 7770</summary>
        [JsonProperty("port")]
        public int Port { get; set; } = EdgegapWindowMetadata.PORT_DEFAULT;
       
        /// <summary>Default "UDP"</summary>
        [JsonProperty("protocol")]
        public string ProtocolStr { get; set; } = EdgegapWindowMetadata.DEFAULT_PROTOCOL_TYPE.ToString();
        
        [JsonProperty("to_check")]
        public bool ToCheck { get; set; } = true;
        
        [JsonProperty("tls_upgrade")]
        public bool TlsUpgrade { get; set; }
        
        [JsonProperty("name")]
        public string PortName { get; set; } = "Game Port";
    }
}
