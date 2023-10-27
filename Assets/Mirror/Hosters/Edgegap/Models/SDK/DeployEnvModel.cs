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
  public class DeployEnvModel {
    /// <summary>
    /// The Key to retrieve the value in your instance
    /// </summary>
    /// <value>The Key to retrieve the value in your instance</value>
    [DataMember(Name="key", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "key")]
    public string Key { get; set; }

    /// <summary>
    /// The value to set in your instance
    /// </summary>
    /// <value>The value to set in your instance</value>
    [DataMember(Name="value", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "value")]
    public string Value { get; set; }

    /// <summary>
    /// If set to true, the value will be encrypted during the process of deployment
    /// </summary>
    /// <value>If set to true, the value will be encrypted during the process of deployment</value>
    [DataMember(Name="is_hidden", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "is_hidden")]
    public bool? IsHidden { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class DeployEnvModel {\n");
      sb.Append("  Key: ").Append(Key).Append("\n");
      sb.Append("  Value: ").Append(Value).Append("\n");
      sb.Append("  IsHidden: ").Append(IsHidden).Append("\n");
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
