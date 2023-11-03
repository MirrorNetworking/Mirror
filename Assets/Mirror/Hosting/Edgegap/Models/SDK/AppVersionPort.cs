using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace IO.Swagger.Model {

  /// <summary>
  /// 
  /// </summary>
  [DataContract]
  public class AppVersionPort {
    /// <summary>
    /// The Port to Expose your service
    /// </summary>
    /// <value>The Port to Expose your service</value>
    [DataMember(Name="port", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "port")]
    public int? Port { get; set; }

    /// <summary>
    /// Available protocols: TCP, UDP, TCP/UDP, HTTP, HTTPS, WS or WSS
    /// </summary>
    /// <value>Available protocols: TCP, UDP, TCP/UDP, HTTP, HTTPS, WS or WSS</value>
    [DataMember(Name="protocol", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "protocol")]
    public string Protocol { get; set; }

    /// <summary>
    /// If the port must be verified by our port validations
    /// </summary>
    /// <value>If the port must be verified by our port validations</value>
    [DataMember(Name="to_check", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "to_check")]
    public bool? ToCheck { get; set; }

    /// <summary>
    /// Enabling with HTTP or WS will inject a sidecar proxy that upgrades the connection with TLS
    /// </summary>
    /// <value>Enabling with HTTP or WS will inject a sidecar proxy that upgrades the connection with TLS</value>
    [DataMember(Name="tls_upgrade", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "tls_upgrade")]
    public bool? TlsUpgrade { get; set; }

    /// <summary>
    /// An optional name for the port for easier handling
    /// </summary>
    /// <value>An optional name for the port for easier handling</value>
    [DataMember(Name="name", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "name")]
    public string Name { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class AppVersionPort {\n");
      sb.Append("  Port: ").Append(Port).Append("\n");
      sb.Append("  Protocol: ").Append(Protocol).Append("\n");
      sb.Append("  ToCheck: ").Append(ToCheck).Append("\n");
      sb.Append("  TlsUpgrade: ").Append(TlsUpgrade).Append("\n");
      sb.Append("  Name: ").Append(Name).Append("\n");
      sb.Append("}\n");
      return sb.ToString();
    }

    /// <summary>
    /// Get the JSON string presentation of the object
    /// </summary>
    /// <returns>JSON string presentation of the object</returns>
    public string ToJson() {
      return JsonConvert.SerializeObject(this, Formatting.Indented);
    }

}
}
