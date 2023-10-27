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
  public class Deployments {
    /// <summary>
    /// List of Active Deployments
    /// </summary>
    /// <value>List of Active Deployments</value>
    [DataMember(Name="data", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "data")]
    public List<Deployment> Data { get; set; }

    /// <summary>
    /// Total Objects in the Database
    /// </summary>
    /// <value>Total Objects in the Database</value>
    [DataMember(Name="total_count", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "total_count")]
    public int? TotalCount { get; set; }

    /// <summary>
    /// Pagination Object
    /// </summary>
    /// <value>Pagination Object</value>
    [DataMember(Name="pagination", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "pagination")]
    public Pagination Pagination { get; set; }

    /// <summary>
    /// Extra Messages for the query
    /// </summary>
    /// <value>Extra Messages for the query</value>
    [DataMember(Name="message", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "message")]
    public List<string> Message { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class Deployments {\n");
      sb.Append("  Data: ").Append(Data).Append("\n");
      sb.Append("  TotalCount: ").Append(TotalCount).Append("\n");
      sb.Append("  Pagination: ").Append(Pagination).Append("\n");
      sb.Append("  Message: ").Append(Message).Append("\n");
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
