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
  public class AppVersionUpdate {
    /// <summary>
    /// The Version Name
    /// </summary>
    /// <value>The Version Name</value>
    [DataMember(Name="name", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "name")]
    public string Name { get; set; }

    /// <summary>
    /// If the Version is active currently in the system
    /// </summary>
    /// <value>If the Version is active currently in the system</value>
    [DataMember(Name="is_active", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "is_active")]
    public bool? IsActive { get; set; }

    /// <summary>
    /// The Repository where the image is (i.e. 'harbor.edgegap.com' or 'docker.io')
    /// </summary>
    /// <value>The Repository where the image is (i.e. 'harbor.edgegap.com' or 'docker.io')</value>
    [DataMember(Name="docker_repository", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "docker_repository")]
    public string DockerRepository { get; set; }

    /// <summary>
    /// The name of your image (i.e. 'edgegap/demo')
    /// </summary>
    /// <value>The name of your image (i.e. 'edgegap/demo')</value>
    [DataMember(Name="docker_image", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "docker_image")]
    public string DockerImage { get; set; }

    /// <summary>
    /// The tag of your image (i.e. '0.1.2')
    /// </summary>
    /// <value>The tag of your image (i.e. '0.1.2')</value>
    [DataMember(Name="docker_tag", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "docker_tag")]
    public string DockerTag { get; set; }

    /// <summary>
    /// The username to access the docker repository
    /// </summary>
    /// <value>The username to access the docker repository</value>
    [DataMember(Name="private_username", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "private_username")]
    public string PrivateUsername { get; set; }

    /// <summary>
    /// The Private Password or Token of the username (We recommend to use a token)
    /// </summary>
    /// <value>The Private Password or Token of the username (We recommend to use a token)</value>
    [DataMember(Name="private_token", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "private_token")]
    public string PrivateToken { get; set; }

    /// <summary>
    /// Units of vCPU needed (1024= 1vcpu)
    /// </summary>
    /// <value>Units of vCPU needed (1024= 1vcpu)</value>
    [DataMember(Name="req_cpu", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "req_cpu")]
    public int? ReqCpu { get; set; }

    /// <summary>
    /// Units of memory in MB needed (1024 = 1GB)
    /// </summary>
    /// <value>Units of memory in MB needed (1024 = 1GB)</value>
    [DataMember(Name="req_memory", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "req_memory")]
    public int? ReqMemory { get; set; }

    /// <summary>
    /// Units of GPU needed (1024= 1 GPU)
    /// </summary>
    /// <value>Units of GPU needed (1024= 1 GPU)</value>
    [DataMember(Name="req_video", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "req_video")]
    public int? ReqVideo { get; set; }

    /// <summary>
    /// The Max duration of the game
    /// </summary>
    /// <value>The Max duration of the game</value>
    [DataMember(Name="max_duration", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "max_duration")]
    public int? MaxDuration { get; set; }

    /// <summary>
    /// Allow to inject ASA Variables
    /// </summary>
    /// <value>Allow to inject ASA Variables</value>
    [DataMember(Name="use_telemetry", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "use_telemetry")]
    public bool? UseTelemetry { get; set; }

    /// <summary>
    /// Allow to inject Context Variables
    /// </summary>
    /// <value>Allow to inject Context Variables</value>
    [DataMember(Name="inject_context_env", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "inject_context_env")]
    public bool? InjectContextEnv { get; set; }

    /// <summary>
    /// ACL Protection is active
    /// </summary>
    /// <value>ACL Protection is active</value>
    [DataMember(Name="whitelisting_active", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "whitelisting_active")]
    public bool? WhitelistingActive { get; set; }

    /// <summary>
    /// Allow faster deployment by caching your container image in every Edge site
    /// </summary>
    /// <value>Allow faster deployment by caching your container image in every Edge site</value>
    [DataMember(Name="force_cache", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "force_cache")]
    public bool? ForceCache { get; set; }

    /// <summary>
    /// Start of the preferred interval for caching your container
    /// </summary>
    /// <value>Start of the preferred interval for caching your container</value>
    [DataMember(Name="cache_min_hour", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "cache_min_hour")]
    public int? CacheMinHour { get; set; }

    /// <summary>
    /// End of the preferred interval for caching your container
    /// </summary>
    /// <value>End of the preferred interval for caching your container</value>
    [DataMember(Name="cache_max_hour", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "cache_max_hour")]
    public int? CacheMaxHour { get; set; }

    /// <summary>
    /// Estimated maximum time in seconds to deploy, after this time we will consider it not working and retry.
    /// </summary>
    /// <value>Estimated maximum time in seconds to deploy, after this time we will consider it not working and retry.</value>
    [DataMember(Name="time_to_deploy", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "time_to_deploy")]
    public int? TimeToDeploy { get; set; }

    /// <summary>
    /// Enable every location available. By enabling this, your request will use every potential location, including those which may require a longer time to deploy. This means that your application may take up to 2 minutes before being up and ready. This functionality does not support ACL and Caching at the moment.
    /// </summary>
    /// <value>Enable every location available. By enabling this, your request will use every potential location, including those which may require a longer time to deploy. This means that your application may take up to 2 minutes before being up and ready. This functionality does not support ACL and Caching at the moment.</value>
    [DataMember(Name="enable_all_locations", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "enable_all_locations")]
    public bool? EnableAllLocations { get; set; }

    /// <summary>
    /// Parameters defining the behavior of a session-based app version. If set, the app is considered to be session-based.
    /// </summary>
    /// <value>Parameters defining the behavior of a session-based app version. If set, the app is considered to be session-based.</value>
    [DataMember(Name="session_config", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "session_config")]
    public AppVersionUpdateSessionConfig SessionConfig { get; set; }

    /// <summary>
    /// Gets or Sets Ports
    /// </summary>
    [DataMember(Name="ports", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "ports")]
    public List<AppVersionPort> Ports { get; set; }

    /// <summary>
    /// Gets or Sets Probe
    /// </summary>
    [DataMember(Name="probe", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "probe")]
    public AppVersionProbe Probe { get; set; }

    /// <summary>
    /// Gets or Sets Envs
    /// </summary>
    [DataMember(Name="envs", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "envs")]
    public List<AppVersionEnv> Envs { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      StringBuilder sb = new StringBuilder();
      sb.Append("class AppVersionUpdate {\n");
      sb.Append("  Name: ").Append(Name).Append("\n");
      sb.Append("  IsActive: ").Append(IsActive).Append("\n");
      sb.Append("  DockerRepository: ").Append(DockerRepository).Append("\n");
      sb.Append("  DockerImage: ").Append(DockerImage).Append("\n");
      sb.Append("  DockerTag: ").Append(DockerTag).Append("\n");
      sb.Append("  PrivateUsername: ").Append(PrivateUsername).Append("\n");
      sb.Append("  PrivateToken: ").Append(PrivateToken).Append("\n");
      sb.Append("  ReqCpu: ").Append(ReqCpu).Append("\n");
      sb.Append("  ReqMemory: ").Append(ReqMemory).Append("\n");
      sb.Append("  ReqVideo: ").Append(ReqVideo).Append("\n");
      sb.Append("  MaxDuration: ").Append(MaxDuration).Append("\n");
      sb.Append("  UseTelemetry: ").Append(UseTelemetry).Append("\n");
      sb.Append("  InjectContextEnv: ").Append(InjectContextEnv).Append("\n");
      sb.Append("  WhitelistingActive: ").Append(WhitelistingActive).Append("\n");
      sb.Append("  ForceCache: ").Append(ForceCache).Append("\n");
      sb.Append("  CacheMinHour: ").Append(CacheMinHour).Append("\n");
      sb.Append("  CacheMaxHour: ").Append(CacheMaxHour).Append("\n");
      sb.Append("  TimeToDeploy: ").Append(TimeToDeploy).Append("\n");
      sb.Append("  EnableAllLocations: ").Append(EnableAllLocations).Append("\n");
      sb.Append("  SessionConfig: ").Append(SessionConfig).Append("\n");
      sb.Append("  Ports: ").Append(Ports).Append("\n");
      sb.Append("  Probe: ").Append(Probe).Append("\n");
      sb.Append("  Envs: ").Append(Envs).Append("\n");
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
