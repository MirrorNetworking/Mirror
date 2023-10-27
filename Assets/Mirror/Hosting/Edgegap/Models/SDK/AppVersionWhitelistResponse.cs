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
  public class AppVersionWhitelistResponse {
    /// <summary>
    /// Gets or Sets WhitelistEntries
    /// </summary>
    [DataMember(Name="whitelist_entries", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "whitelist_entries")]
    public List<AppVersionWhitelistEntry> WhitelistEntries { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class AppVersionWhitelistResponse {\n");
      sb.Append("  WhitelistEntries: ").Append(WhitelistEntries).Append("\n");
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
