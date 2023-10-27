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
  public class SessionContext {
    /// <summary>
    /// Unique UUID
    /// </summary>
    /// <value>Unique UUID</value>
    [DataMember(Name="session_id", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "session_id")]
    public string SessionId { get; set; }

    /// <summary>
    /// Custom ID if Available
    /// </summary>
    /// <value>Custom ID if Available</value>
    [DataMember(Name="custom_id", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "custom_id")]
    public string CustomId { get; set; }

    /// <summary>
    /// Current status of the session
    /// </summary>
    /// <value>Current status of the session</value>
    [DataMember(Name="status", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "status")]
    public string Status { get; set; }

    /// <summary>
    /// If the session is linked to a Ready deployment
    /// </summary>
    /// <value>If the session is linked to a Ready deployment</value>
    [DataMember(Name="ready", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "ready")]
    public bool? Ready { get; set; }

    /// <summary>
    /// If the session is linked to a deployment
    /// </summary>
    /// <value>If the session is linked to a deployment</value>
    [DataMember(Name="linked", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "linked")]
    public bool? Linked { get; set; }

    /// <summary>
    /// Type of session created
    /// </summary>
    /// <value>Type of session created</value>
    [DataMember(Name="kind", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "kind")]
    public string Kind { get; set; }

    /// <summary>
    /// Count of user this session currently have
    /// </summary>
    /// <value>Count of user this session currently have</value>
    [DataMember(Name="user_count", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "user_count")]
    public int? UserCount { get; set; }

    /// <summary>
    /// Unique UUID
    /// </summary>
    /// <value>Unique UUID</value>
    [DataMember(Name="deployment_request_id", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "deployment_request_id")]
    public string DeploymentRequestId { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class SessionContext {\n");
      sb.Append("  SessionId: ").Append(SessionId).Append("\n");
      sb.Append("  CustomId: ").Append(CustomId).Append("\n");
      sb.Append("  Status: ").Append(Status).Append("\n");
      sb.Append("  Ready: ").Append(Ready).Append("\n");
      sb.Append("  Linked: ").Append(Linked).Append("\n");
      sb.Append("  Kind: ").Append(Kind).Append("\n");
      sb.Append("  UserCount: ").Append(UserCount).Append("\n");
      sb.Append("  DeploymentRequestId: ").Append(DeploymentRequestId).Append("\n");
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
