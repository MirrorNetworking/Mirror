using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models.Results
{
    /// <summary>Edgegap error, generally just containing `message`</summary>
    public class EdgegapErrorResult 
    {
        /// <summary>Friendly, UI-facing error message from Edgegap; can be lengthy.</summary>
        [JsonProperty("message")]
        public string ErrorMessage { get; set; }
    }
}
