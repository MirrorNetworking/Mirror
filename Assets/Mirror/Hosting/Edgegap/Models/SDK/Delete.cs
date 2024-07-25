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
  public class Delete {
    /// <summary>
    /// A message depending of the request termination
    /// </summary>
    /// <value>A message depending of the request termination</value>
    [DataMember(Name="message", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "message")]
    public string Message { get; set; }

    /// <summary>
    /// The status/summary of the deployment
    /// </summary>
    /// <value>The status/summary of the deployment</value>
    [DataMember(Name="deployment_summary", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "deployment_summary")]
    public Status DeploymentSummary { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class Delete {\n");
      sb.Append("  Message: ").Append(Message).Append("\n");
      sb.Append("  DeploymentSummary: ").Append(DeploymentSummary).Append("\n");
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
