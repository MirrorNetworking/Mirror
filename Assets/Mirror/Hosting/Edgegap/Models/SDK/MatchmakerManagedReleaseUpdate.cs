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
  public class MatchmakerManagedReleaseUpdate : MatchmakerReleaseUpdateBase {
    /// <summary>
    /// Name of the matchmaker release configuration to use for this managed release.
    /// </summary>
    /// <value>Name of the matchmaker release configuration to use for this managed release.</value>
    [DataMember(Name="release_config_name", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "release_config_name")]
    public string ReleaseConfigName { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class MatchmakerManagedReleaseUpdate {\n");
      sb.Append("  ReleaseConfigName: ").Append(ReleaseConfigName).Append("\n");
      sb.Append("}\n");
      return sb.ToString();
    }

    /// <summary>
    /// Get the JSON string presentation of the object
    /// </summary>
    /// <returns>JSON string presentation of the object</returns>
    public  new string ToJson() {
      return JsonConvert.SerializeObject(this, Formatting.Indented);
    }

}
}
