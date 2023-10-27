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
  public class BulkSessionDelete {
    /// <summary>
    /// List of Delete
    /// </summary>
    /// <value>List of Delete</value>
    [DataMember(Name="sessions", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "sessions")]
    public List<SessionDelete> Sessions { get; set; }

    /// <summary>
    /// List of Delete Errors
    /// </summary>
    /// <value>List of Delete Errors</value>
    [DataMember(Name="errors", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "errors")]
    public List<string> Errors { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class BulkSessionDelete {\n");
      sb.Append("  Sessions: ").Append(Sessions).Append("\n");
      sb.Append("  Errors: ").Append(Errors).Append("\n");
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
