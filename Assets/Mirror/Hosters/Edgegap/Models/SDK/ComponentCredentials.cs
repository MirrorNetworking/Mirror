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
  public class ComponentCredentials {
    /// <summary>
    /// Username for the component's private repository.
    /// </summary>
    /// <value>Username for the component's private repository.</value>
    [DataMember(Name="username", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "username")]
    public string Username { get; set; }

    /// <summary>
    /// Password (token) of the component's private repository.
    /// </summary>
    /// <value>Password (token) of the component's private repository.</value>
    [DataMember(Name="token", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "token")]
    public string Token { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class ComponentCredentials {\n");
      sb.Append("  Username: ").Append(Username).Append("\n");
      sb.Append("  Token: ").Append(Token).Append("\n");
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
