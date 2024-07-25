using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models.Requests
{
    /// <summary>
    /// Request model for https://docs.edgegap.com/api/#tag/Applications/operation/application-post
    /// </summary>
    public class CreateAppRequest
    {
        #region Required
        /// <summary>*The application name.</summary>
        [JsonProperty("name")]
        public string AppName { get; set; }
        #endregion // Required
        
        
        #region Optional
        /// <summary>*If the application can be deployed.</summary>
        [JsonProperty("is_active")]
        public bool IsActive { get; set; }
        
        /// <summary>*Image base64 string.</summary>
        [JsonProperty("image")]
        public string Image { get; set; }
        
        /// <summary>If the telemetry agent is installed on the versions of this app.</summary>
        [JsonProperty("is_telemetry_agent_active")]
        public bool IsTelemetryAgentActive { get; set; }
        #endregion // Optional


        /// <summary>Used by Newtonsoft</summary>
        public CreateAppRequest()
        {
        }
        
        /// <summary>Init with required info</summary>
        /// <param name="appName">The application name</param>
        /// <param name="isActive">If the application can be deployed</param>
        /// <param name="image">Image base64 string</param>
        public CreateAppRequest(
            string appName,
            bool isActive,
            string image)
        {
            this.AppName = appName;
            this.IsActive = isActive;
            this.Image = image;
        }

        /// <summary>Parse to json str</summary>
        public override string ToString() =>
            JsonConvert.SerializeObject(this);
    }
}
