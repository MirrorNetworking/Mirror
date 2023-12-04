using Newtonsoft.Json;

namespace Edgegap
{
    public struct AppVersionUpdatePatchData
    {
        [JsonProperty("docker_repository")]
        public string DockerRegistry;

        [JsonProperty("docker_image")]
        public string DockerImage;

        [JsonProperty("docker_tag")]
        public string DockerTag;
    }
}
