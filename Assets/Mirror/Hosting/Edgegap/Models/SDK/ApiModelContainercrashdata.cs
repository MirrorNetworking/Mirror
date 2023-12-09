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
  public class ApiModelContainercrashdata {
    /// <summary>
    /// Auto Generated Field for exit_code
    /// </summary>
    /// <value>Auto Generated Field for exit_code</value>
    [DataMember(Name="exit_code", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "exit_code")]
    public int? ExitCode { get; set; }

    /// <summary>
    /// Auto Generated Field for message
    /// </summary>
    /// <value>Auto Generated Field for message</value>
    [DataMember(Name="message", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "message")]
    public string Message { get; set; }

    /// <summary>
    /// Auto Generated Field for restart_count
    /// </summary>
    /// <value>Auto Generated Field for restart_count</value>
    [DataMember(Name="restart_count", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "restart_count")]
    public int? RestartCount { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class ApiModelContainercrashdata {\n");
      sb.Append("  ExitCode: ").Append(ExitCode).Append("\n");
      sb.Append("  Message: ").Append(Message).Append("\n");
      sb.Append("  RestartCount: ").Append(RestartCount).Append("\n");
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
