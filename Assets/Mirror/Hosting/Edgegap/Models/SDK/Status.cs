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
  public class Status {
    /// <summary>
    /// The Unique ID of the Deployment's request
    /// </summary>
    /// <value>The Unique ID of the Deployment's request</value>
    [DataMember(Name="request_id", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "request_id")]
    public string RequestId { get; set; }

    /// <summary>
    /// The FQDN that allow to connect to your Deployment
    /// </summary>
    /// <value>The FQDN that allow to connect to your Deployment</value>
    [DataMember(Name="fqdn", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "fqdn")]
    public string Fqdn { get; set; }

    /// <summary>
    /// The name of the deployed App
    /// </summary>
    /// <value>The name of the deployed App</value>
    [DataMember(Name="app_name", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "app_name")]
    public string AppName { get; set; }

    /// <summary>
    /// The version of the deployed App
    /// </summary>
    /// <value>The version of the deployed App</value>
    [DataMember(Name="app_version", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "app_version")]
    public string AppVersion { get; set; }

    /// <summary>
    /// The current status of the Deployment
    /// </summary>
    /// <value>The current status of the Deployment</value>
    [DataMember(Name="current_status", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "current_status")]
    public string CurrentStatus { get; set; }

    /// <summary>
    /// True if the current Deployment is ready to be connected and running
    /// </summary>
    /// <value>True if the current Deployment is ready to be connected and running</value>
    [DataMember(Name="running", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "running")]
    public bool? Running { get; set; }

    /// <summary>
    /// True if the current Deployment is ACL protected
    /// </summary>
    /// <value>True if the current Deployment is ACL protected</value>
    [DataMember(Name="whitelisting_active", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "whitelisting_active")]
    public bool? WhitelistingActive { get; set; }

    /// <summary>
    /// Timestamp of the Deployment when it is up and running
    /// </summary>
    /// <value>Timestamp of the Deployment when it is up and running</value>
    [DataMember(Name="start_time", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "start_time")]
    public string StartTime { get; set; }

    /// <summary>
    /// Timestamp of the end of the Deployment
    /// </summary>
    /// <value>Timestamp of the end of the Deployment</value>
    [DataMember(Name="removal_time", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "removal_time")]
    public string RemovalTime { get; set; }

    /// <summary>
    /// Time since the Deployment is up and running in seconds
    /// </summary>
    /// <value>Time since the Deployment is up and running in seconds</value>
    [DataMember(Name="elapsed_time", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "elapsed_time")]
    public int? ElapsedTime { get; set; }

    /// <summary>
    /// The last status of the Deployment
    /// </summary>
    /// <value>The last status of the Deployment</value>
    [DataMember(Name="last_status", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "last_status")]
    public string LastStatus { get; set; }

    /// <summary>
    /// True if there is an error with the Deployment
    /// </summary>
    /// <value>True if there is an error with the Deployment</value>
    [DataMember(Name="error", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "error")]
    public bool? Error { get; set; }

    /// <summary>
    /// The error details of the Deployment
    /// </summary>
    /// <value>The error details of the Deployment</value>
    [DataMember(Name="error_detail", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "error_detail")]
    public string ErrorDetail { get; set; }

    /// <summary>
    /// Gets or Sets Ports
    /// </summary>
    [DataMember(Name="ports", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "ports")]
    public Dictionary<string, PortMapping> Ports { get; set; }

    /// <summary>
    /// The public IP
    /// </summary>
    /// <value>The public IP</value>
    [DataMember(Name="public_ip", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "public_ip")]
    public string PublicIp { get; set; }

    /// <summary>
    /// List of Active Sessions if Deployment App is Session Based
    /// </summary>
    /// <value>List of Active Sessions if Deployment App is Session Based</value>
    [DataMember(Name="sessions", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "sessions")]
    public List<DeploymentSessionContext> Sessions { get; set; }

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
      sb.Append("class Status {\n");
      sb.Append("  RequestId: ").Append(RequestId).Append("\n");
      sb.Append("  Fqdn: ").Append(Fqdn).Append("\n");
      sb.Append("  AppName: ").Append(AppName).Append("\n");
      sb.Append("  AppVersion: ").Append(AppVersion).Append("\n");
      sb.Append("  CurrentStatus: ").Append(CurrentStatus).Append("\n");
      sb.Append("  Running: ").Append(Running).Append("\n");
      sb.Append("  WhitelistingActive: ").Append(WhitelistingActive).Append("\n");
      sb.Append("  StartTime: ").Append(StartTime).Append("\n");
      sb.Append("  RemovalTime: ").Append(RemovalTime).Append("\n");
      sb.Append("  ElapsedTime: ").Append(ElapsedTime).Append("\n");
      sb.Append("  LastStatus: ").Append(LastStatus).Append("\n");
      sb.Append("  Error: ").Append(Error).Append("\n");
      sb.Append("  ErrorDetail: ").Append(ErrorDetail).Append("\n");
      sb.Append("  Ports: ").Append(Ports).Append("\n");
      sb.Append("  PublicIp: ").Append(PublicIp).Append("\n");
      sb.Append("  Sessions: ").Append(Sessions).Append("\n");
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
