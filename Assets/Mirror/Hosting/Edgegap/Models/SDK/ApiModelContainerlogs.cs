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
  public class ApiModelContainerlogs {
    /// <summary>
    /// Auto Generated Field for logs
    /// </summary>
    /// <value>Auto Generated Field for logs</value>
    [DataMember(Name="logs", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "logs")]
    public string Logs { get; set; }

    /// <summary>
    /// Auto Generated Field for encoding
    /// </summary>
    /// <value>Auto Generated Field for encoding</value>
    [DataMember(Name="encoding", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "encoding")]
    public string Encoding { get; set; }

    /// <summary>
    /// Auto Generated Field for crash_logs
    /// </summary>
    /// <value>Auto Generated Field for crash_logs</value>
    [DataMember(Name="crash_logs", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "crash_logs")]
    public string CrashLogs { get; set; }

    /// <summary>
    /// Gets or Sets CrashData
    /// </summary>
    [DataMember(Name="crash_data", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "crash_data")]
    public ApiModelContainercrashdata CrashData { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class ApiModelContainerlogs {\n");
      sb.Append("  Logs: ").Append(Logs).Append("\n");
      sb.Append("  Encoding: ").Append(Encoding).Append("\n");
      sb.Append("  CrashLogs: ").Append(CrashLogs).Append("\n");
      sb.Append("  CrashData: ").Append(CrashData).Append("\n");
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
