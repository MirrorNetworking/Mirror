using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models.Requests
{
    /// <summary>
    /// Request model for `PATCH v1/app/{app_name}/version/{version_name}`.
    /// Request model for https://docs.edgegap.com/api/#tag/Applications/operation/app-versions-patch
    /// TODO: Split "Create" and "Update" into their own, separate models: CTRL+F for "(!)" for more info.
    /// </summary>
    public class UpdateAppVersionRequest
    {
        #region Required
        /// <summary>*Required: The name of the application.</summary>
        [JsonIgnore] // *Path var
        public string AppName { get; set; }
        #endregion // Required
        
        
        #region Optional
        /// <summary>The name of the application version.</summary>
        [JsonIgnore] // *Path var
        public string VersionName { get; set; } = EdgegapWindowMetadata.DEFAULT_VERSION_TAG;
        
        /// <summary>At least 1 { Port, ProtocolStr }</summary>
        [JsonProperty("ports")]
        public AppPortsData[] Ports { get; set; } = {};
        
        /// <summary>The Repository where the image is.</summary>
        /// <example>"registry.edgegap.com" || "harbor.edgegap.com" || "docker.io"</example>
        [JsonProperty("docker_repository")]
        public string DockerRepository { get; set; } = "";

        /// <summary>The name of your image.</summary>
        /// <example>"edgegap/demo" || "myCompany-someId/mylowercaseapp"</example>
        [JsonProperty("docker_image")]
        public string DockerImage { get; set; } = "";
       
        /// <summary>The tag of your image. Default == "latest".</summary>
        /// <example>"0.1.2" || "latest" (although "latest" !recommended; use actual versions in production)</example>
        [JsonProperty("docker_tag")]
        public string DockerTag { get; set; } = EdgegapWindowMetadata.DEFAULT_VERSION_TAG;
       
        [JsonProperty("is_active")]
        public bool IsActive { get; set; } = true;
       
        [JsonProperty("private_username")]
        public string PrivateUsername { get; set; } = "";
       
        [JsonProperty("private_token")]
        public string PrivateToken { get; set; } = "";
       
        #region (!) Shows in API docs for PATCH, but could be CREATE only? "Unknown Args"
        // [JsonProperty("req_cpu")]
        // public int ReqCpu { get; set; } = 256;
        //
        // [JsonProperty("req_memory")]
        // public int ReqMemory { get; set; } = 256;
        //
        // [JsonProperty("req_video")]
        // public int ReqVideo { get; set; } = 256;
        #endregion // (!) Shows in API docs for PATCH, but could be CREATE only? "Unknown Args"
       
        [JsonProperty("max_duration")]
        public int MaxDuration { get; set; } = 60;
       
        [JsonProperty("use_telemetry")]
        public bool UseTelemetry { get; set; } = true;
       
        [JsonProperty("inject_context_env")]
        public bool InjectContextEnv { get; set; } = true;
       
        [JsonProperty("whitelisting_active")]
        public bool WhitelistingActive { get; set; } = false;
       
        [JsonProperty("force_cache")]
        public bool ForceCache { get; set; }
       
        [JsonProperty("cache_min_hour")]
        public int CacheMinHour { get; set; }
       
        [JsonProperty("cache_max_hour")]
        public int CacheMaxHour { get; set; }
       
        [JsonProperty("time_to_deploy")]
        public int TimeToDeploy { get; set; } = 120;
       
        [JsonProperty("enable_all_locations")]
        public bool EnableAllLocations { get; set; }
       
        [JsonProperty("termination_grace_period_seconds")]
        public int TerminationGracePeriodSeconds { get; set; } = 5;
       
        // // (!) BUG: Expects empty string "" at minimum; however, empty string will throw server err
        // [JsonProperty("endpoint_storage")]
        // public string EndpointStorage { get; set; }
       
        [JsonProperty("command")]
        public string Command { get; set; }
       
        [JsonProperty("arguments")]
        public string Arguments { get; set; }
       
        // /// <summary>
        // /// (!) Setting this will trigger a very specific type of game that will affect the AppVersion.
        // /// TODO: Is leaving as null the same as commenting out?
        // /// </summary>
        // [JsonProperty("session_config")]
        // public SessionConfigData SessionConfig { get; set; }
       
        [JsonProperty("probe")]
        public ProbeData Probe { get; set; } = new ProbeData(); // MIRROR CHANGE: 'new()' not supported in Unity 2020

        [JsonProperty("envs")]
        public EnvsData[] Envs { get; set; } = {};
       
        public class SessionConfigData
        {
            [JsonProperty("kind")]
            public string Kind { get; set; } = "Seat";
       
            [JsonProperty("sockets")]
            public int Sockets { get; set; } = 10;
       
            [JsonProperty("autodeploy")]
            public bool Autodeploy { get; set; } = true;
       
            [JsonProperty("empty_ttl")]
            public int EmptyTtl { get; set; } = 60;
       
            [JsonProperty("session_max_duration")]
            public int SessionMaxDuration { get; set; } = 60;
        }

        public class ProbeData
        {
            [JsonProperty("optimal_ping")]
            public int OptimalPing { get; set; } = 60;
       
            [JsonProperty("rejected_ping")]
            public int RejectedPing { get; set; } = 180;
        }
       
        public class EnvsData
        {
            [JsonProperty("key")]
            public string Key { get; set; }
       
            [JsonProperty("value")]
            public string Value { get; set; }
       
            [JsonProperty("is_secret")]
            public bool IsSecret { get; set; } = true;
        }
        #endregion // Optional

        /// <summary>Used by Newtonsoft</summary>
        public UpdateAppVersionRequest()
        {
        }
        
        /// <summary>
        /// Init with required info. Default version/tag == "default".
        /// Since we're updating, we only require the AppName.
        /// </summary>
        /// <param name="appName">The name of the application.</param>
        public UpdateAppVersionRequest(string appName)
        {
            this.AppName = appName;
        }
        
        /// <summary>Parse to json str</summary>
        public override string ToString() =>
            JsonConvert.SerializeObject(this);
    }
}
