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
  public class TotalMetricsModel {
    /// <summary>
    /// Gets or Sets ReceiveTotal
    /// </summary>
    [DataMember(Name="receive_total", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "receive_total")]
    public MetricsModel ReceiveTotal { get; set; }

    /// <summary>
    /// Gets or Sets TransmitTotal
    /// </summary>
    [DataMember(Name="transmit_total", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "transmit_total")]
    public MetricsModel TransmitTotal { get; set; }

    /// <summary>
    /// Gets or Sets DiskReadTotal
    /// </summary>
    [DataMember(Name="disk_read_total", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "disk_read_total")]
    public MetricsModel DiskReadTotal { get; set; }

    /// <summary>
    /// Gets or Sets DiskWriteTotal
    /// </summary>
    [DataMember(Name="disk_write_total", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "disk_write_total")]
    public MetricsModel DiskWriteTotal { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class TotalMetricsModel {\n");
      sb.Append("  ReceiveTotal: ").Append(ReceiveTotal).Append("\n");
      sb.Append("  TransmitTotal: ").Append(TransmitTotal).Append("\n");
      sb.Append("  DiskReadTotal: ").Append(DiskReadTotal).Append("\n");
      sb.Append("  DiskWriteTotal: ").Append(DiskWriteTotal).Append("\n");
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
