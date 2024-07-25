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
  public class Locations {
    /// <summary>
    /// Gets or Sets _Locations
    /// </summary>
    [DataMember(Name="locations", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "locations")]
    public List<Location> _Locations { get; set; }

    /// <summary>
    /// Extra Messages for the query
    /// </summary>
    /// <value>Extra Messages for the query</value>
    [DataMember(Name="messages", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "messages")]
    public List<string> Messages { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class Locations {\n");
      sb.Append("  _Locations: ").Append(_Locations).Append("\n");
      sb.Append("  Messages: ").Append(Messages).Append("\n");
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
