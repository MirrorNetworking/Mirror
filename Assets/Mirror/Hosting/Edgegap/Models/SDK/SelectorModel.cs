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
  public class SelectorModel {
    /// <summary>
    /// The Tag to filter potential Deployment with this Selector
    /// </summary>
    /// <value>The Tag to filter potential Deployment with this Selector</value>
    [DataMember(Name="tag", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "tag")]
    public string Tag { get; set; }

    /// <summary>
    /// If True, will not try to filter Deployment and only tag the Session
    /// </summary>
    /// <value>If True, will not try to filter Deployment and only tag the Session</value>
    [DataMember(Name="tag_only", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "tag_only")]
    public bool? TagOnly { get; set; }

    /// <summary>
    /// Environment Variable to inject in new Deployment created by App Version with auto-deploy
    /// </summary>
    /// <value>Environment Variable to inject in new Deployment created by App Version with auto-deploy</value>
    [DataMember(Name="env", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "env")]
    public Object Env { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class SelectorModel {\n");
      sb.Append("  Tag: ").Append(Tag).Append("\n");
      sb.Append("  TagOnly: ").Append(TagOnly).Append("\n");
      sb.Append("  Env: ").Append(Env).Append("\n");
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
