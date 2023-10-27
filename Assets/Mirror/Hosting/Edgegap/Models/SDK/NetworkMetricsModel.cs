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
  public class NetworkMetricsModel {
    /// <summary>
    /// Gets or Sets Receive
    /// </summary>
    [DataMember(Name="receive", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "receive")]
    public MetricsModel Receive { get; set; }

    /// <summary>
    /// Gets or Sets Transmit
    /// </summary>
    [DataMember(Name="transmit", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "transmit")]
    public MetricsModel Transmit { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class NetworkMetricsModel {\n");
      sb.Append("  Receive: ").Append(Receive).Append("\n");
      sb.Append("  Transmit: ").Append(Transmit).Append("\n");
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
