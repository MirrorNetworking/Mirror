using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
