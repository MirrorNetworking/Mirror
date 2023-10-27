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
  public class AppVersionEnv {
    /// <summary>
    /// The Key where the Environment Variable will be set
    /// </summary>
    /// <value>The Key where the Environment Variable will be set</value>
    [DataMember(Name="key", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "key")]
    public string Key { get; set; }

    /// <summary>
    /// The Value to retrieve at the Key
    /// </summary>
    /// <value>The Value to retrieve at the Key</value>
    [DataMember(Name="value", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "value")]
    public string Value { get; set; }

    /// <summary>
    /// This will encrypt the value in our database, won't be readable by external source
    /// </summary>
    /// <value>This will encrypt the value in our database, won't be readable by external source</value>
    [DataMember(Name="is_secret", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "is_secret")]
    public bool? IsSecret { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class AppVersionEnv {\n");
      sb.Append("  Key: ").Append(Key).Append("\n");
      sb.Append("  Value: ").Append(Value).Append("\n");
      sb.Append("  IsSecret: ").Append(IsSecret).Append("\n");
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
