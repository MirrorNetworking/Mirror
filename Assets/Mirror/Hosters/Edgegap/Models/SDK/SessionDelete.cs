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
  public class SessionDelete {
    /// <summary>
    /// A message depending of the request termination
    /// </summary>
    /// <value>A message depending of the request termination</value>
    [DataMember(Name="message", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "message")]
    public string Message { get; set; }

    /// <summary>
    /// The Unique Identifier of the Session
    /// </summary>
    /// <value>The Unique Identifier of the Session</value>
    [DataMember(Name="session_id", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "session_id")]
    public string SessionId { get; set; }

    /// <summary>
    /// Custom ID if Available
    /// </summary>
    /// <value>Custom ID if Available</value>
    [DataMember(Name="custom_id", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "custom_id")]
    public string CustomId { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class SessionDelete {\n");
      sb.Append("  Message: ").Append(Message).Append("\n");
      sb.Append("  SessionId: ").Append(SessionId).Append("\n");
      sb.Append("  CustomId: ").Append(CustomId).Append("\n");
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
