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
  public class AppVersionUpdateSessionConfig {
    /// <summary>
    /// The kind of session to create. If 'Default' if chosen, the application will be handled like a normal application. The kind of session must be: Default, Seat, Match
    /// </summary>
    /// <value>The kind of session to create. If 'Default' if chosen, the application will be handled like a normal application. The kind of session must be: Default, Seat, Match</value>
    [DataMember(Name="kind", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "kind")]
    public string Kind { get; set; }

    /// <summary>
    /// The number of game slots on each deployment of this app version.
    /// </summary>
    /// <value>The number of game slots on each deployment of this app version.</value>
    [DataMember(Name="sockets", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "sockets")]
    public int? Sockets { get; set; }

    /// <summary>
    /// If a deployment should be made autonomously if there is not enough sockets open on a new session.
    /// </summary>
    /// <value>If a deployment should be made autonomously if there is not enough sockets open on a new session.</value>
    [DataMember(Name="autodeploy", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "autodeploy")]
    public bool? Autodeploy { get; set; }

    /// <summary>
    /// The number of minutes a deployment of this app version can spend with no session connected before being terminated.
    /// </summary>
    /// <value>The number of minutes a deployment of this app version can spend with no session connected before being terminated.</value>
    [DataMember(Name="empty_ttl", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "empty_ttl")]
    public int? EmptyTtl { get; set; }

    /// <summary>
    /// The number of minutes after a session-type deployment has been terminated to remove all the session information connected to your deployment. Minimum and default value is set to 60 minutes so you can manage your session termination before it is removed.
    /// </summary>
    /// <value>The number of minutes after a session-type deployment has been terminated to remove all the session information connected to your deployment. Minimum and default value is set to 60 minutes so you can manage your session termination before it is removed.</value>
    [DataMember(Name="session_max_duration", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "session_max_duration")]
    public int? SessionMaxDuration { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class AppVersionUpdateSessionConfig {\n");
      sb.Append("  Kind: ").Append(Kind).Append("\n");
      sb.Append("  Sockets: ").Append(Sockets).Append("\n");
      sb.Append("  Autodeploy: ").Append(Autodeploy).Append("\n");
      sb.Append("  EmptyTtl: ").Append(EmptyTtl).Append("\n");
      sb.Append("  SessionMaxDuration: ").Append(SessionMaxDuration).Append("\n");
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
