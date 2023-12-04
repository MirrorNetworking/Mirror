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
  public class StaticSites {
    /// <summary>
    /// The URL to bind to
    /// </summary>
    /// <value>The URL to bind to</value>
    [DataMember(Name="url", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "url")]
    public string Url { get; set; }

    /// <summary>
    /// Public IP of the Site
    /// </summary>
    /// <value>Public IP of the Site</value>
    [DataMember(Name="public_ip", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "public_ip")]
    public string PublicIp { get; set; }

    /// <summary>
    /// Port to Bind to
    /// </summary>
    /// <value>Port to Bind to</value>
    [DataMember(Name="port", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "port")]
    public decimal? Port { get; set; }

    /// <summary>
    /// Latitude of the Site
    /// </summary>
    /// <value>Latitude of the Site</value>
    [DataMember(Name="latitude", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "latitude")]
    public decimal? Latitude { get; set; }

    /// <summary>
    /// Longitude of the Site
    /// </summary>
    /// <value>Longitude of the Site</value>
    [DataMember(Name="longitude", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "longitude")]
    public decimal? Longitude { get; set; }

    /// <summary>
    /// City of the Site
    /// </summary>
    /// <value>City of the Site</value>
    [DataMember(Name="city", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "city")]
    public string City { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class StaticSites {\n");
      sb.Append("  Url: ").Append(Url).Append("\n");
      sb.Append("  PublicIp: ").Append(PublicIp).Append("\n");
      sb.Append("  Port: ").Append(Port).Append("\n");
      sb.Append("  Latitude: ").Append(Latitude).Append("\n");
      sb.Append("  Longitude: ").Append(Longitude).Append("\n");
      sb.Append("  City: ").Append(City).Append("\n");
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
