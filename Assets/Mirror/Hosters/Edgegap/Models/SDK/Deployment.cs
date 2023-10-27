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
  public class Deployment {
    /// <summary>
    /// Unique UUID
    /// </summary>
    /// <value>Unique UUID</value>
    [DataMember(Name="request_id", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "request_id")]
    public string RequestId { get; set; }

    /// <summary>
    /// The public IP
    /// </summary>
    /// <value>The public IP</value>
    [DataMember(Name="public_ip", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "public_ip")]
    public string PublicIp { get; set; }

    /// <summary>
    /// Current status of Deployment
    /// </summary>
    /// <value>Current status of Deployment</value>
    [DataMember(Name="status", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "status")]
    public string Status { get; set; }

    /// <summary>
    /// if the deployment is ready
    /// </summary>
    /// <value>if the deployment is ready</value>
    [DataMember(Name="ready", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "ready")]
    public bool? Ready { get; set; }

    /// <summary>
    /// if the deployment ACL is active
    /// </summary>
    /// <value>if the deployment ACL is active</value>
    [DataMember(Name="whitelisting_active", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "whitelisting_active")]
    public bool? WhitelistingActive { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <value></value>
    [DataMember(Name="fqdn", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "fqdn")]
    public string Fqdn { get; set; }

    /// <summary>
    /// Gets or Sets Ports
    /// </summary>
    [DataMember(Name="ports", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "ports")]
    public Dictionary<string, PortMapping> Ports { get; set; }

    /// <summary>
    /// Location related information
    /// </summary>
    /// <value>Location related information</value>
    [DataMember(Name="location", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "location")]
    public DeploymentLocation Location { get; set; }

    /// <summary>
    /// List of tags associated with the deployment
    /// </summary>
    /// <value>List of tags associated with the deployment</value>
    [DataMember(Name="tags", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "tags")]
    public List<string> Tags { get; set; }

    /// <summary>
    /// The Capacity of the Deployment
    /// </summary>
    /// <value>The Capacity of the Deployment</value>
    [DataMember(Name="sockets", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "sockets")]
    public int? Sockets { get; set; }

    /// <summary>
    /// The Capacity Usage of the Deployment
    /// </summary>
    /// <value>The Capacity Usage of the Deployment</value>
    [DataMember(Name="sockets_usage", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "sockets_usage")]
    public int? SocketsUsage { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class Deployment {\n");
      sb.Append("  RequestId: ").Append(RequestId).Append("\n");
      sb.Append("  PublicIp: ").Append(PublicIp).Append("\n");
      sb.Append("  Status: ").Append(Status).Append("\n");
      sb.Append("  Ready: ").Append(Ready).Append("\n");
      sb.Append("  WhitelistingActive: ").Append(WhitelistingActive).Append("\n");
      sb.Append("  Fqdn: ").Append(Fqdn).Append("\n");
      sb.Append("  Ports: ").Append(Ports).Append("\n");
      sb.Append("  Location: ").Append(Location).Append("\n");
      sb.Append("  Tags: ").Append(Tags).Append("\n");
      sb.Append("  Sockets: ").Append(Sockets).Append("\n");
      sb.Append("  SocketsUsage: ").Append(SocketsUsage).Append("\n");
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
