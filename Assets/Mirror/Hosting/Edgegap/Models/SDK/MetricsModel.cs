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
  public class MetricsModel {
    /// <summary>
    /// Gets or Sets Labels
    /// </summary>
    [DataMember(Name="labels", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "labels")]
    public List<string> Labels { get; set; }

    /// <summary>
    /// Gets or Sets Datasets
    /// </summary>
    [DataMember(Name="datasets", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "datasets")]
    public List<decimal?> Datasets { get; set; }

    /// <summary>
    /// Gets or Sets Timestamps
    /// </summary>
    [DataMember(Name="timestamps", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "timestamps")]
    public List<DateTime?> Timestamps { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class MetricsModel {\n");
      sb.Append("  Labels: ").Append(Labels).Append("\n");
      sb.Append("  Datasets: ").Append(Datasets).Append("\n");
      sb.Append("  Timestamps: ").Append(Timestamps).Append("\n");
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
