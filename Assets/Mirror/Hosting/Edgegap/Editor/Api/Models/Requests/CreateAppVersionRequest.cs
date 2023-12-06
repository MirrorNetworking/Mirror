using System;
using Newtonsoft.Json;
using UnityEngine;

namespace Edgegap.Editor.Api.Models.Requests
{
    /// <summary>
    /// Request model for `POST v1/app/{app_name}/version`.
    /// API Doc | https://docs.edgegap.com/api/#tag/Applications/operation/app-version-post
    /// </summary>
    public class CreateAppVersionRequest
    {
        #region Required
        /// <summary>*The name of the application associated.</summary>
        [JsonIgnore] // *Path var
        public string AppName { get; set; }
        
        /// <summary>*The name of the application associated.</summary>
        [JsonProperty("name")]
        public string VersionName { get; set; } = EdgegapWindowMetadata.DEFAULT_VERSION_TAG;
        
        /// <summary>*The tag of your image. Default == "latest".</summary>
        /// <example>"0.1.2" || "latest" (although "latest" !recommended; use actual versions in production)</example>
        [JsonProperty("docker_tag")]
        public string DockerTag { get; set; } = EdgegapWindowMetadata.DEFAULT_VERSION_TAG;
        
        /// <summary>*The name of your image.</summary>
        /// <example>"edgegap/demo" || "myCompany-someId/mylowercaseapp"</example>
        [JsonProperty("docker_image")]
        public string DockerImage { get; set; } = "";

        /// <summary>*The Repository where the image is.</summary>
        /// <example>"registry.edgegap.com" || "harbor.edgegap.com" || "docker.io"</example>
        [JsonProperty("docker_repository")]
        public string DockerRepository { get; set; } = "";
        
        /// <summary>*Units of vCPU needed (1024 = 1vcpu)</summary>
        [JsonProperty("req_cpu")]
        public int ReqCpu { get; set; } = 256;
       
        /// <summary>*Units of memory in MB needed (1024 = 1 GPU)</summary>
        [JsonProperty("req_memory")]
        public int ReqMemory { get; set; } = 256;

        /// <summary>*Required: At least 1 { Port, ProtocolStr }.</summary>
        [JsonProperty("ports")]
        public AppPortsData[] Ports { get; set; } = {};
        
        /// <summary>The username to access the docker repository</summary>
        [JsonProperty("private_username")]
        public string PrivateUsername { get; set; } = "";
        
        /// <summary>The Private Password or Token of the username (We recommend to use a token)</summary>
        [JsonProperty("private_token")]
        public string PrivateToken { get; set; } = "";
        #endregion // Required
        
        
        // #region Optional
        // [JsonProperty("is_active")]
        // public bool IsActive { get; set; } = true;
        //
        // [JsonProperty("req_video")]
        // public int ReqVideo { get; set; } = 256;
        //
        // [JsonProperty("max_duration")]
        // public int MaxDuration { get; set; } = 30;
        //
        // [JsonProperty("use_telemetry")]
        // public bool UseTelemetry { get; set; } = true;
        //
        // [JsonProperty("inject_context_env")]
        // public bool InjectContextEnv { get; set; } = true;
        //
        // [JsonProperty("whitelisting_active")]
        // public bool WhitelistingActive { get; set; } = true;
        //
        // [JsonProperty("force_cache")]
        // public bool ForceCache { get; set; }
        //
        // [JsonProperty("cache_min_hour")]
        // public int CacheMinHour { get; set; }
        //
        // [JsonProperty("cache_max_hour")]
        // public int CacheMaxHour { get; set; }
        //
        // [JsonProperty("time_to_deploy")]
        // public int TimeToDeploy { get; set; } = 15;
        //
        // [JsonProperty("enable_all_locations")]
        // public bool EnableAllLocations { get; set; }
        //
        // [JsonProperty("termination_grace_period_seconds")]
        // public int TerminationGracePeriodSeconds { get; set; } = 5;
        //
        // [JsonProperty("endpoint_storage")]
        // public string EndpointStorage { get; set; } = "";
        //
        // [JsonProperty("command")]
        // public string Command { get; set; }
        //
        // [JsonProperty("arguments")]
        // public string Arguments { get; set; }
        //
        // [JsonProperty("verify_image")]
        // public bool VerifyImage { get; set; }
        //
        // [JsonProperty("session_config")]
        // public SessionConfigData SessionConfig { get; set; } = new();
        //
        // [JsonProperty("probe")]
        // public ProbeData Probe { get; set; } = new();
        //
        // [JsonProperty("envs")]
        // public EnvsData[] Envs { get; set; } = {};
        //
        // public class SessionConfigData
        // {
        //     [JsonProperty("kind")]
        //     public string Kind { get; set; } = "Seat";
        //
        //     [JsonProperty("sockets")]
        //     public int Sockets { get; set; } = 10;
        //
        //     [JsonProperty("autodeploy")]
        //     public bool Autodeploy { get; set; } = true;
        //
        //     [JsonProperty("empty_ttl")]
        //     public int EmptyTtl { get; set; } = 60;
        //
        //     [JsonProperty("session_max_duration")]
        //     public int SessionMaxDuration { get; set; } = 60;
        // }
        //
        //
        // public class ProbeData
        // {
        //     [JsonProperty("optimal_ping")]
        //     public int OptimalPing { get; set; } = 60;
        //
        //     [JsonProperty("rejected_ping")]
        //     public int RejectedPing { get; set; } = 180;
        // }
        //
        // public class EnvsData
        // {
        //     [JsonProperty("key")]
        //     public string Key { get; set; }
        //
        //     [JsonProperty("value")]
        //     public string Value { get; set; }
        //
        //     [JsonProperty("is_secret")]
        //     public bool IsSecret { get; set; } = true;
        // }
        // #endregion // Optional

        /// <summary>Used by Newtonsoft</summary>
        public CreateAppVersionRequest()
        {
        }
        
        /// <summary>
        /// Init with required info.
        /// (!) If looking for refs, also see FromUpdateRequest() builder below.
        /// </summary>
        /// <param name="appName">The name of the application.</param>
        /// <param name="containerRegistryUsername"></param>
        /// <param name="containerRegistryPasswordToken"></param>
        /// <param name="portNum"></param>
        /// <param name="protocolType"></param>
        public CreateAppVersionRequest(
            string appName,
            string containerRegistryUsername,
            string containerRegistryPasswordToken,
            int portNum,
            ProtocolType protocolType)
        {
            this.AppName = appName;
            this.PrivateUsername = containerRegistryUsername;
            this.PrivateToken = containerRegistryPasswordToken;
            this.Ports = new AppPortsData[]
            {
                new AppPortsData() // MIRROR CHANGE: 'new()' not supported in Unity 2020
                {
                    Port = portNum,
                    ProtocolStr = protocolType.ToString(),
                },
            };
        }

        /// <summary>
        /// Port from Update PATCH model: If you tried to Update, but !exists, you probably want to create it next.
        /// </summary>
        /// <param name="updateRequest"></param>
        public static CreateAppVersionRequest FromUpdateRequest(UpdateAppVersionRequest updateRequest)
        {
            // Convert the updateRequest to JSON
            string json = JsonConvert.SerializeObject(updateRequest);

            // Deserialize the JSON back to CreateAppVersionRequest
            CreateAppVersionRequest createReq = null;
                
            try
            {
                createReq = JsonConvert.DeserializeObject<CreateAppVersionRequest>(json);
                createReq.AppName = updateRequest.AppName; // Normally JsonIgnored in Update
                createReq.VersionName = updateRequest.VersionName; // Normally JsonIgnored in Update
                createReq.PrivateUsername = updateRequest.PrivateUsername;
                createReq.PrivateToken = updateRequest.PrivateToken;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error (when parsing CreateAppVersionRequest from CreateAppVersionRequest): {e}");
                throw;
            }

            return createReq;
        }

        /// <summary>Parse to json str</summary>
        public override string ToString() =>
            JsonConvert.SerializeObject(this);
    }
}
