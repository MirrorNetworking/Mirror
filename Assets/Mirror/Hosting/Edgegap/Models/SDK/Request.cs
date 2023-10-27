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
  public class Request {
    /// <summary>
    /// The Unique Identifier of the request
    /// </summary>
    /// <value>The Unique Identifier of the request</value>
    [DataMember(Name="request_id", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "request_id")]
    public string RequestId { get; set; }

    /// <summary>
    /// The URL to connect to the instance
    /// </summary>
    /// <value>The URL to connect to the instance</value>
    [DataMember(Name="request_dns", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "request_dns")]
    public string RequestDns { get; set; }

    /// <summary>
    /// The Name of the App you requested
    /// </summary>
    /// <value>The Name of the App you requested</value>
    [DataMember(Name="request_app", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "request_app")]
    public string RequestApp { get; set; }

    /// <summary>
    /// The name of the App Version you requested
    /// </summary>
    /// <value>The name of the App Version you requested</value>
    [DataMember(Name="request_version", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "request_version")]
    public string RequestVersion { get; set; }

    /// <summary>
    /// How Many Users your request contain
    /// </summary>
    /// <value>How Many Users your request contain</value>
    [DataMember(Name="request_user_count", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "request_user_count")]
    public int? RequestUserCount { get; set; }

    /// <summary>
    /// The city where the deployment is located
    /// </summary>
    /// <value>The city where the deployment is located</value>
    [DataMember(Name="city", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "city")]
    public string City { get; set; }

    /// <summary>
    /// The country where the deployment is located
    /// </summary>
    /// <value>The country where the deployment is located</value>
    [DataMember(Name="country", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "country")]
    public string Country { get; set; }

    /// <summary>
    /// The continent where the deployment is located
    /// </summary>
    /// <value>The continent where the deployment is located</value>
    [DataMember(Name="continent", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "continent")]
    public string Continent { get; set; }

    /// <summary>
    /// The administrative division where the deployment is located
    /// </summary>
    /// <value>The administrative division where the deployment is located</value>
    [DataMember(Name="administrative_division", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "administrative_division")]
    public string AdministrativeDivision { get; set; }

    /// <summary>
    /// List of tags associated with the deployment
    /// </summary>
    /// <value>List of tags associated with the deployment</value>
    [DataMember(Name="tags", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "tags")]
    public List<string> Tags { get; set; }

    /// <summary>
    /// The container log storage options for the deployment
    /// </summary>
    /// <value>The container log storage options for the deployment</value>
    [DataMember(Name="container_log_storage", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "container_log_storage")]
    public ContainerLogStorageModel ContainerLogStorage { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class Request {\n");
      sb.Append("  RequestId: ").Append(RequestId).Append("\n");
      sb.Append("  RequestDns: ").Append(RequestDns).Append("\n");
      sb.Append("  RequestApp: ").Append(RequestApp).Append("\n");
      sb.Append("  RequestVersion: ").Append(RequestVersion).Append("\n");
      sb.Append("  RequestUserCount: ").Append(RequestUserCount).Append("\n");
      sb.Append("  City: ").Append(City).Append("\n");
      sb.Append("  Country: ").Append(Country).Append("\n");
      sb.Append("  Continent: ").Append(Continent).Append("\n");
      sb.Append("  AdministrativeDivision: ").Append(AdministrativeDivision).Append("\n");
      sb.Append("  Tags: ").Append(Tags).Append("\n");
      sb.Append("  ContainerLogStorage: ").Append(ContainerLogStorage).Append("\n");
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
