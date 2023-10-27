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
  public class MatchmakerReleaseCreate : MatchmakerReleaseCreateBase {
    /// <summary>
    /// Name of the matchmaker component to use as the Open Match frontend.
    /// </summary>
    /// <value>Name of the matchmaker component to use as the Open Match frontend.</value>
    [DataMember(Name="frontend_component_name", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "frontend_component_name")]
    public string FrontendComponentName { get; set; }

    /// <summary>
    /// Name of the matchmaker component to use as the Open Match director.
    /// </summary>
    /// <value>Name of the matchmaker component to use as the Open Match director.</value>
    [DataMember(Name="director_component_name", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "director_component_name")]
    public string DirectorComponentName { get; set; }

    /// <summary>
    /// Name of the matchmaker component to use as the Open Match match function.
    /// </summary>
    /// <value>Name of the matchmaker component to use as the Open Match match function.</value>
    [DataMember(Name="match_function_component_name", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "match_function_component_name")]
    public string MatchFunctionComponentName { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class MatchmakerReleaseCreate {\n");
      sb.Append("  FrontendComponentName: ").Append(FrontendComponentName).Append("\n");
      sb.Append("  DirectorComponentName: ").Append(DirectorComponentName).Append("\n");
      sb.Append("  MatchFunctionComponentName: ").Append(MatchFunctionComponentName).Append("\n");
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
