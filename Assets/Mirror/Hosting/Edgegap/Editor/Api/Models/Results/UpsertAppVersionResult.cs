using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models.Results
{
    /// <summary>
    /// Result model for:
    /// - `POST 1/app/{app_name}/version`
    /// - `PATCH v1/app/{app_name}/version/{version_name}`
    /// POST API Doc | https://docs.edgegap.com/api/#tag/Applications/operation/application-post
    /// PATCH API Doc | https://docs.edgegap.com/api/#tag/Applications/operation/app-versions-patch
    /// </summary>
    public class UpsertAppVersionResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("version")]
        public VersionData Version { get; set; }

        public class VersionData
        {
            [JsonProperty("name")]
            public string VersionName { get; set; }

            [JsonProperty("is_active")]
            public bool IsActive { get; set; }

            [JsonProperty("docker_repository")]
            public string DockerRepository { get; set; }

            [JsonProperty("docker_image")]
            public string DockerImage { get; set; }

            [JsonProperty("docker_tag")]
            public string DockerTag { get; set; }

            [JsonProperty("private_username")]
            public string PrivateUsername { get; set; }

            [JsonProperty("private_token")]
            public string PrivateToken { get; set; }

            [JsonProperty("req_cpu")]
            public int? ReqCpu { get; set; }

            [JsonProperty("req_memory")]
            public int? ReqMemory { get; set; }

            [JsonProperty("req_video")]
            public int? ReqVideo { get; set; }
            
            [JsonProperty("max_duration")]
            public int? MaxDuration { get; set; }

            [JsonProperty("use_telemetry")]
            public bool UseTelemetry { get; set; }

            [JsonProperty("inject_context_env")]
            public bool InjectContextEnv { get; set; }

            [JsonProperty("whitelisting_active")]
            public bool WhitelistingActive { get; set; }

            [JsonProperty("force_cache")]
            public bool ForceCache { get; set; }

            [JsonProperty("cache_min_hour")]
            public int? CacheMinHour { get; set; }

            [JsonProperty("cache_max_hour")]
            public int? CacheMaxHour { get; set; }

            [JsonProperty("time_to_deploy")]
            public int? TimeToDeploy { get; set; }

            [JsonProperty("enable_all_locations")]
            public bool EnableAllLocations { get; set; }

            [JsonProperty("session_config")]
            public SessionConfigData SessionConfig { get; set; }

            [JsonProperty("ports")]
            public PortsData[] Ports { get; set; }

            [JsonProperty("probe")]
            public ProbeData Probe { get; set; }

            [JsonProperty("envs")]
            public EnvsData[] Envs { get; set; }

            [JsonProperty("verify_image")]
            public bool VerifyImage { get; set; }

            [JsonProperty("termination_grace_period_seconds")]
            public int? TerminationGracePeriodSeconds { get; set; }

            [JsonProperty("endpoint_storage")]
            public string EndpointStorage { get; set; }

            [JsonProperty("command")]
            public string Command { get; set; }

            [JsonProperty("arguments")]
            public string Arguments { get; set; }
        }

        public class SessionConfigData
        {
            [JsonProperty("kind")]
            public string Kind { get; set; }

            [JsonProperty("sockets")]
            public int? Sockets { get; set; }

            [JsonProperty("autodeploy")]
            public bool Autodeploy { get; set; }

            [JsonProperty("empty_ttl")]
            public int? EmptyTtl { get; set; }

            [JsonProperty("session_max_duration")]
            public int? SessionMaxDuration { get; set; }
        }

        public class PortsData
        {
            [JsonProperty("port")]
            public int? Port { get; set; }

            [JsonProperty("protocol")]
            public string Protocol { get; set; }

            [JsonProperty("to_check")]
            public bool ToCheck { get; set; }

            [JsonProperty("tls_upgrade")]
            public bool TlsUpgrade { get; set; }

            [JsonProperty("name")]
            public string PortName { get; set; }
        }

        public class ProbeData
        {
            [JsonProperty("optimal_ping")]
            public int? OptimalPing { get; set; }

            [JsonProperty("rejected_ping")]
            public int? RejectedPing { get; set; }
        }

        public class EnvsData
        {
            [JsonProperty("key")]
            public string Key { get; set; }

            [JsonProperty("value")]
            public string Value { get; set; }

            [JsonProperty("is_secret")]
            public bool IsSecret { get; set; }
        }

    }
}
