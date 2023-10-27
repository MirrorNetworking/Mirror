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
  public class PortMapping {
    /// <summary>
    /// The Port to Connect from Internet
    /// </summary>
    /// <value>The Port to Connect from Internet</value>
    [DataMember(Name="external", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "external")]
    public int? External { get; set; }

    /// <summary>
    /// The internal Port of the Container
    /// </summary>
    /// <value>The internal Port of the Container</value>
    [DataMember(Name="internal", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "internal")]
    public int? Internal { get; set; }

    /// <summary>
    /// The Protocol (i.e. 'TCP')
    /// </summary>
    /// <value>The Protocol (i.e. 'TCP')</value>
    [DataMember(Name="protocol", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "protocol")]
    public string Protocol { get; set; }

    /// <summary>
    /// The Name of the port if given, default to internal port in string
    /// </summary>
    /// <value>The Name of the port if given, default to internal port in string</value>
    [DataMember(Name="name", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "name")]
    public string Name { get; set; }

    /// <summary>
    /// If the port require TLS Upgrade
    /// </summary>
    /// <value>If the port require TLS Upgrade</value>
    [DataMember(Name="tls_upgrade", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "tls_upgrade")]
    public bool? TlsUpgrade { get; set; }

    /// <summary>
    /// link of the port with scheme depending of the protocol
    /// </summary>
    /// <value>link of the port with scheme depending of the protocol</value>
    [DataMember(Name="link", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "link")]
    public string Link { get; set; }

    /// <summary>
    /// Internal Proxy Mapping
    /// </summary>
    /// <value>Internal Proxy Mapping</value>
    [DataMember(Name="proxy", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "proxy")]
    public int? Proxy { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class PortMapping {\n");
      sb.Append("  External: ").Append(External).Append("\n");
      sb.Append("  Internal: ").Append(Internal).Append("\n");
      sb.Append("  Protocol: ").Append(Protocol).Append("\n");
      sb.Append("  Name: ").Append(Name).Append("\n");
      sb.Append("  TlsUpgrade: ").Append(TlsUpgrade).Append("\n");
      sb.Append("  Link: ").Append(Link).Append("\n");
      sb.Append("  Proxy: ").Append(Proxy).Append("\n");
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
