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
  public class DeployModel {
    /// <summary>
    /// The Name of the App you want to deploy
    /// </summary>
    /// <value>The Name of the App you want to deploy</value>
    [DataMember(Name="app_name", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "app_name")]
    public string AppName { get; set; }

    /// <summary>
    /// The Name of the App Version you want to deploy, if not present, the last version created is picked
    /// </summary>
    /// <value>The Name of the App Version you want to deploy, if not present, the last version created is picked</value>
    [DataMember(Name="version_name", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "version_name")]
    public string VersionName { get; set; }

    /// <summary>
    /// If the Application is public or private. If not specified, we will look for a private Application
    /// </summary>
    /// <value>If the Application is public or private. If not specified, we will look for a private Application</value>
    [DataMember(Name="is_public_app", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "is_public_app")]
    public bool? IsPublicApp { get; set; }

    /// <summary>
    /// The List of IP of your user
    /// </summary>
    /// <value>The List of IP of your user</value>
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
    /// A list of deployment variables
    /// </summary>
    /// <value>A list of deployment variables</value>
    [DataMember(Name="env_vars", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "env_vars")]
    public List<DeployEnvModel> EnvVars { get; set; }

    /// <summary>
    /// If you want to skip the Telemetry and use a geolocations decision only
    /// </summary>
    /// <value>If you want to skip the Telemetry and use a geolocations decision only</value>
    [DataMember(Name="skip_telemetry", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "skip_telemetry")]
    public bool? SkipTelemetry { get; set; }

    /// <summary>
    /// If you want to specify a centroid for your deployment.
    /// </summary>
    /// <value>If you want to specify a centroid for your deployment.</value>
    [DataMember(Name="location", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "location")]
    public LocationModel Location { get; set; }

    /// <summary>
    /// If you want to deploy in a specific city
    /// </summary>
    /// <value>If you want to deploy in a specific city</value>
    [DataMember(Name="city", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "city")]
    public string City { get; set; }

    /// <summary>
    /// If you want to deploy in a specific country
    /// </summary>
    /// <value>If you want to deploy in a specific country</value>
    [DataMember(Name="country", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "country")]
    public string Country { get; set; }

    /// <summary>
    /// If you want to deploy in a specific continent
    /// </summary>
    /// <value>If you want to deploy in a specific continent</value>
    [DataMember(Name="continent", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "continent")]
    public string Continent { get; set; }

    /// <summary>
    /// If you want to deploy in a specific region
    /// </summary>
    /// <value>If you want to deploy in a specific region</value>
    [DataMember(Name="region", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "region")]
    public string Region { get; set; }

    /// <summary>
    /// If you want to deploy in a specific administrative division
    /// </summary>
    /// <value>If you want to deploy in a specific administrative division</value>
    [DataMember(Name="administrative_division", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "administrative_division")]
    public string AdministrativeDivision { get; set; }

    /// <summary>
    /// A web URL. This url will be called with method POST. The deployment status will be send in JSON format
    /// </summary>
    /// <value>A web URL. This url will be called with method POST. The deployment status will be send in JSON format</value>
    [DataMember(Name="webhook_url", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "webhook_url")]
    public string WebhookUrl { get; set; }

    /// <summary>
    /// The list of tags for your deployment
    /// </summary>
    /// <value>The list of tags for your deployment</value>
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
      sb.Append("class DeployModel {\n");
      sb.Append("  AppName: ").Append(AppName).Append("\n");
      sb.Append("  VersionName: ").Append(VersionName).Append("\n");
      sb.Append("  IsPublicApp: ").Append(IsPublicApp).Append("\n");
      sb.Append("  IpList: ").Append(IpList).Append("\n");
      sb.Append("  GeoIpList: ").Append(GeoIpList).Append("\n");
      sb.Append("  EnvVars: ").Append(EnvVars).Append("\n");
      sb.Append("  SkipTelemetry: ").Append(SkipTelemetry).Append("\n");
      sb.Append("  Location: ").Append(Location).Append("\n");
      sb.Append("  City: ").Append(City).Append("\n");
      sb.Append("  Country: ").Append(Country).Append("\n");
      sb.Append("  Continent: ").Append(Continent).Append("\n");
      sb.Append("  Region: ").Append(Region).Append("\n");
      sb.Append("  AdministrativeDivision: ").Append(AdministrativeDivision).Append("\n");
      sb.Append("  WebhookUrl: ").Append(WebhookUrl).Append("\n");
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
