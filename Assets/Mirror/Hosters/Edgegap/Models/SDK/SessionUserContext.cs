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
  public class SessionUserContext {
    /// <summary>
    /// Users in the session
    /// </summary>
    /// <value>Users in the session</value>
    [DataMember(Name="session_users", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "session_users")]
    public List<SessionUser> SessionUsers { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class SessionUserContext {\n");
      sb.Append("  SessionUsers: ").Append(SessionUsers).Append("\n");
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
