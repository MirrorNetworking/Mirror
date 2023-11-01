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
  public class PatchSessionModel {
    /// <summary>
    /// The List of IP of your user, Array of String, example:     [\"162.254.103.13\",\"198.12.116.39\", \"162.254.135.39\", \"162.254.129.34\"]
    /// </summary>
    /// <value>The List of IP of your user, Array of String, example:     [\"162.254.103.13\",\"198.12.116.39\", \"162.254.135.39\", \"162.254.129.34\"]</value>
    [DataMember(Name="ip_list", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "ip_list")]
    public List<string> IpList { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      var sb = new StringBuilder();
      sb.Append("class PatchSessionModel {\n");
      sb.Append("  IpList: ").Append(IpList).Append("\n");
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
