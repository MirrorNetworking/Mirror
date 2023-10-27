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
  public class ApplicationPatch {
    /// <summary>
    /// Application name
    /// </summary>
    /// <value>Application name</value>
    [DataMember(Name="name", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "name")]
    public string Name { get; set; }

    /// <summary>
    /// If the application can be deployed
    /// </summary>
    /// <value>If the application can be deployed</value>
    [DataMember(Name="is_active", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "is_active")]
    public bool? IsActive { get; set; }

    /// <summary>
    /// Image base64 string
    /// </summary>
    /// <value>Image base64 string</value>
    [DataMember(Name="image", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "image")]
    public string Image { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class ApplicationPatch {\n");
      sb.Append("  Name: ").Append(Name).Append("\n");
      sb.Append("  IsActive: ").Append(IsActive).Append("\n");
      sb.Append("  Image: ").Append(Image).Append("\n");
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
