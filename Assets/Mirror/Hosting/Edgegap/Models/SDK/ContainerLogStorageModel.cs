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
  public class ContainerLogStorageModel {
    /// <summary>
    /// Will override the app version container log storage for this deployment
    /// </summary>
    /// <value>Will override the app version container log storage for this deployment</value>
    [DataMember(Name="enabled", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "enabled")]
    public bool? Enabled { get; set; }

    /// <summary>
    /// The name of your endpoint storage. If container log storage is enabled without this parameter, we will try to take the app version endpoint storage. If there is no endpoint storage in your app version, the container logs will not be stored. If we don't find any endpoint storage associated with this name, the container logs will not be stored.
    /// </summary>
    /// <value>The name of your endpoint storage. If container log storage is enabled without this parameter, we will try to take the app version endpoint storage. If there is no endpoint storage in your app version, the container logs will not be stored. If we don't find any endpoint storage associated with this name, the container logs will not be stored.</value>
    [DataMember(Name="endpoint_storage", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "endpoint_storage")]
    public string EndpointStorage { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class ContainerLogStorageModel {\n");
      sb.Append("  Enabled: ").Append(Enabled).Append("\n");
      sb.Append("  EndpointStorage: ").Append(EndpointStorage).Append("\n");
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
