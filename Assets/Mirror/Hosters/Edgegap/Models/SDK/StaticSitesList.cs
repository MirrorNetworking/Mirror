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
  public class StaticSitesList {
    /// <summary>
    /// Gets or Sets Sites
    /// </summary>
    [DataMember(Name="sites", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "sites")]
    public List<StaticSites> Sites { get; set; }

    /// <summary>
    /// Total of Sites
    /// </summary>
    /// <value>Total of Sites</value>
    [DataMember(Name="total_count", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "total_count")]
    public int? TotalCount { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class StaticSitesList {\n");
      sb.Append("  Sites: ").Append(Sites).Append("\n");
      sb.Append("  TotalCount: ").Append(TotalCount).Append("\n");
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
