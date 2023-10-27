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
  public class MatchmakerReleaseConfigUpdate {
    /// <summary>
    /// Matchmaker configuration name. Must be unique.
    /// </summary>
    /// <value>Matchmaker configuration name. Must be unique.</value>
    [DataMember(Name="name", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "name")]
    public string Name { get; set; }

    /// <summary>
    /// Matchmaker configuration, parsed as a string.
    /// </summary>
    /// <value>Matchmaker configuration, parsed as a string.</value>
    [DataMember(Name="configuration", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "configuration")]
    public string Configuration { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class MatchmakerReleaseConfigUpdate {\n");
      sb.Append("  Name: ").Append(Name).Append("\n");
      sb.Append("  Configuration: ").Append(Configuration).Append("\n");
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
