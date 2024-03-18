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
  public class SessionModel {
    /// <summary>
    /// The Name of the App you want to deploy, example:    supermario
    /// </summary>
    /// <value>The Name of the App you want to deploy, example:    supermario</value>
    [DataMember(Name="app_name", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "app_name")]
    public string AppName { get; set; }

    /// <summary>
    /// The Name of the App Version you want to deploy, example:    v1.0
    /// </summary>
    /// <value>The Name of the App Version you want to deploy, example:    v1.0</value>
    [DataMember(Name="version_name", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "version_name")]
    public string VersionName { get; set; }

    /// <summary>
    /// The List of IP of your user, Array of String, example:     [\"162.254.103.13\",\"198.12.116.39\", \"162.254.135.39\", \"162.254.129.34\"]
    /// </summary>
    /// <value>The List of IP of your user, Array of String, example:     [\"162.254.103.13\",\"198.12.116.39\", \"162.254.135.39\", \"162.254.129.34\"]</value>
    [DataMember(Name="ip_list", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "ip_list")]
    public List<string> IpList { get; set; }

    /// <summary>
    /// The list of IP of your user with their location (latitude, longitude)
    /// </summary>
    /// <value>The list of IP of your user with their location (latitude, longitude)</value>
    [DataMember(Name="geo_ip_list", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "geo_ip_list")]
    public List<GeoIpListModel> GeoIpList { get; set; }

    /// <summary>
    /// The request id of your deployment. If specified, the session will link to the deployment
    /// </summary>
    /// <value>The request id of your deployment. If specified, the session will link to the deployment</value>
    [DataMember(Name="deployment_request_id", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "deployment_request_id")]
    public string DeploymentRequestId { get; set; }

    /// <summary>
    /// If you want to specify a centroid for your session.
    /// </summary>
    /// <value>If you want to specify a centroid for your session.</value>
    [DataMember(Name="location", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "location")]
    public LocationModel Location { get; set; }

    /// <summary>
    /// If you want your session in a specific city
    /// </summary>
    /// <value>If you want your session in a specific city</value>
    [DataMember(Name="city", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "city")]
    public string City { get; set; }

    /// <summary>
    /// If you want your session in a specific country
    /// </summary>
    /// <value>If you want your session in a specific country</value>
    [DataMember(Name="country", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "country")]
    public string Country { get; set; }

    /// <summary>
    /// If you want your session in a specific continent
    /// </summary>
    /// <value>If you want your session in a specific continent</value>
    [DataMember(Name="continent", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "continent")]
    public string Continent { get; set; }

    /// <summary>
    /// If you want your session in a specific administrative division
    /// </summary>
    /// <value>If you want your session in a specific administrative division</value>
    [DataMember(Name="administrative_division", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "administrative_division")]
    public string AdministrativeDivision { get; set; }

    /// <summary>
    /// If you want your session in a specific region
    /// </summary>
    /// <value>If you want your session in a specific region</value>
    [DataMember(Name="region", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "region")]
    public string Region { get; set; }

    /// <summary>
    /// List of Selectors to filter potential Deployment to link and tag the Session
    /// </summary>
    /// <value>List of Selectors to filter potential Deployment to link and tag the Session</value>
    [DataMember(Name="selectors", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "selectors")]
    public List<SelectorModel> Selectors { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class SessionModel {\n");
      sb.Append("  AppName: ").Append(AppName).Append("\n");
      sb.Append("  VersionName: ").Append(VersionName).Append("\n");
      sb.Append("  IpList: ").Append(IpList).Append("\n");
      sb.Append("  GeoIpList: ").Append(GeoIpList).Append("\n");
      sb.Append("  DeploymentRequestId: ").Append(DeploymentRequestId).Append("\n");
      sb.Append("  Location: ").Append(Location).Append("\n");
      sb.Append("  City: ").Append(City).Append("\n");
      sb.Append("  Country: ").Append(Country).Append("\n");
      sb.Append("  Continent: ").Append(Continent).Append("\n");
      sb.Append("  AdministrativeDivision: ").Append(AdministrativeDivision).Append("\n");
      sb.Append("  Region: ").Append(Region).Append("\n");
      sb.Append("  Selectors: ").Append(Selectors).Append("\n");
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
