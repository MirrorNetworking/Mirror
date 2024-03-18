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
  public class MatchmakerReleaseResponseBase : BaseModel {
    /// <summary>
    /// Name of the app to deploy using the matchmaker.
    /// </summary>
    /// <value>Name of the app to deploy using the matchmaker.</value>
    [DataMember(Name="app_name", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "app_name")]
    public string AppName { get; set; }

    /// <summary>
    /// Name of the version of the specified app to deploy using the matchmaker.
    /// </summary>
    /// <value>Name of the version of the specified app to deploy using the matchmaker.</value>
    [DataMember(Name="version_name", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "version_name")]
    public string VersionName { get; set; }

    /// <summary>
    /// Name of the matchmaker release. Should be unique, and will be used to differentiate your releases.
    /// </summary>
    /// <value>Name of the matchmaker release. Should be unique, and will be used to differentiate your releases.</value>
    [DataMember(Name="version", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "version")]
    public string Version { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class MatchmakerReleaseResponseBase {\n");
      sb.Append("  AppName: ").Append(AppName).Append("\n");
      sb.Append("  VersionName: ").Append(VersionName).Append("\n");
      sb.Append("  Version: ").Append(Version).Append("\n");
      sb.Append("}\n");
      return sb.ToString();
    }

    /// <summary>
    /// Get the JSON string presentation of the object
    /// </summary>
    /// <returns>JSON string presentation of the object</returns>
    public  new string ToJson() {
      return JsonConvert.SerializeObject(this, Formatting.Indented);
    }

}
}
