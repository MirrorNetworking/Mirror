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
  public class Monitor {
    /// <summary>
    /// API Name
    /// </summary>
    /// <value>API Name</value>
    [DataMember(Name="name", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "name")]
    public string Name { get; set; }

    /// <summary>
    /// API Version
    /// </summary>
    /// <value>API Version</value>
    [DataMember(Name="version", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "version")]
    public string Version { get; set; }

    /// <summary>
    /// API Host
    /// </summary>
    /// <value>API Host</value>
    [DataMember(Name="host", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "host")]
    public string Host { get; set; }

    /// <summary>
    /// API Host URL
    /// </summary>
    /// <value>API Host URL</value>
    [DataMember(Name="host_url", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "host_url")]
    public string HostUrl { get; set; }

    /// <summary>
    /// API Swagger Specification Location
    /// </summary>
    /// <value>API Swagger Specification Location</value>
    [DataMember(Name="spec_url", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "spec_url")]
    public string SpecUrl { get; set; }

    /// <summary>
    /// API Messages
    /// </summary>
    /// <value>API Messages</value>
    [DataMember(Name="messages", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "messages")]
    public List<string> Messages { get; set; }

    /// <summary>
    /// API Errors
    /// </summary>
    /// <value>API Errors</value>
    [DataMember(Name="errors", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "errors")]
    public List<string> Errors { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class Monitor {\n");
      sb.Append("  Name: ").Append(Name).Append("\n");
      sb.Append("  Version: ").Append(Version).Append("\n");
      sb.Append("  Host: ").Append(Host).Append("\n");
      sb.Append("  HostUrl: ").Append(HostUrl).Append("\n");
      sb.Append("  SpecUrl: ").Append(SpecUrl).Append("\n");
      sb.Append("  Messages: ").Append(Messages).Append("\n");
      sb.Append("  Errors: ").Append(Errors).Append("\n");
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
