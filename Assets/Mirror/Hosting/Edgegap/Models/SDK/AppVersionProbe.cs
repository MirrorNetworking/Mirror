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
  public class AppVersionProbe {
    /// <summary>
    /// Your optimal value for Latency
    /// </summary>
    /// <value>Your optimal value for Latency</value>
    [DataMember(Name="optimal_ping", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "optimal_ping")]
    public int? OptimalPing { get; set; }

    /// <summary>
    /// Your reject value for Latency
    /// </summary>
    /// <value>Your reject value for Latency</value>
    [DataMember(Name="rejected_ping", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "rejected_ping")]
    public int? RejectedPing { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class AppVersionProbe {\n");
      sb.Append("  OptimalPing: ").Append(OptimalPing).Append("\n");
      sb.Append("  RejectedPing: ").Append(RejectedPing).Append("\n");
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
