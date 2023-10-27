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
  public class AppVersionWhitelistEntrySuccess {
    /// <summary>
    /// if the operation succeed
    /// </summary>
    /// <value>if the operation succeed</value>
    [DataMember(Name="success", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "success")]
    public bool? Success { get; set; }

    /// <summary>
    /// Gets or Sets WhitelistEntry
    /// </summary>
    [DataMember(Name="whitelist_entry", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "whitelist_entry")]
    public AppVersionWhitelistEntry WhitelistEntry { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class AppVersionWhitelistEntrySuccess {\n");
      sb.Append("  Success: ").Append(Success).Append("\n");
      sb.Append("  WhitelistEntry: ").Append(WhitelistEntry).Append("\n");
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
