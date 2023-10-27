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
  public class SessionRequest {
    /// <summary>
    /// The Unique Identifier of the Session
    /// </summary>
    /// <value>The Unique Identifier of the Session</value>
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
    /// The Name of the App you requested
    /// </summary>
    /// <value>The Name of the App you requested</value>
    [DataMember(Name="app", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "app")]
    public string App { get; set; }

    /// <summary>
    /// The name of the App Version you requested
    /// </summary>
    /// <value>The name of the App Version you requested</value>
    [DataMember(Name="version", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "version")]
    public string Version { get; set; }

    /// <summary>
    /// The Name of the App Version you want to deploy, example:    v1.0
    /// </summary>
    /// <value>The Name of the App Version you want to deploy, example:    v1.0</value>
    [DataMember(Name="deployment_request_id", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "deployment_request_id")]
    public string DeploymentRequestId { get; set; }

    /// <summary>
    /// List of Selectors to filter potential Deployment to link and tag the Session
    /// </summary>
    /// <value>List of Selectors to filter potential Deployment to link and tag the Session</value>
    [DataMember(Name="selectors", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "selectors")]
    public List<SelectorModel> Selectors { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class SessionRequest {\n");
      sb.Append("  SessionId: ").Append(SessionId).Append("\n");
      sb.Append("  CustomId: ").Append(CustomId).Append("\n");
      sb.Append("  App: ").Append(App).Append("\n");
      sb.Append("  Version: ").Append(Version).Append("\n");
      sb.Append("  DeploymentRequestId: ").Append(DeploymentRequestId).Append("\n");
      sb.Append("  Selectors: ").Append(Selectors).Append("\n");
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
