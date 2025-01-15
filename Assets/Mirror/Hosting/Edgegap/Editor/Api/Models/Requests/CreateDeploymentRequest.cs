using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models.Requests
{
    /// <summary>
    /// Request model for `POST v1/deploy`.
    /// API Doc | https://docs.edgegap.com/api/#tag/Deployments/operation/deploy
    /// </summary>
    public class CreateDeploymentRequest
    {
        #region Required
        /// <summary>*Required: The name of the App you want to deploy.</summary>
        [JsonProperty("app_name")]
        public string AppName { get; set; }
        
        /// <summary>
        /// *Required: The name of the App Version you want to deploy;
        /// if not present, the last version created is picked.
        /// </summary>
        [JsonProperty("version_name")]
        public string VersionName { get; set; }
        
        /// <summary>
        /// *Required: The List of IP of your user.
        /// </summary>
        [JsonProperty("ip_list")]
        public string[] IpList { get; set; }

        /// <summary>
        /// *Required: The list of IP of your user with their location (latitude, longitude).
        /// </summary>
        [JsonProperty("geo_ip_list")]
        public string[] GeoIpList { get; set; } = {};
        #endregion // Required
        
        
        /// <summary>Used by Newtonsoft</summary>
        public CreateDeploymentRequest()
        {
        }

        /// <summary>Init with required info; used for a single external IP address.</summary>
        /// <param name="appName">The name of the application.</param>
        /// <param name="versionName">
        /// The name of the App Version you want to deploy, if not present,
        /// the last version created is picked.
        /// </param>
        /// <param name="externalIp">Obtain from IpApi.</param>
        public CreateDeploymentRequest(
            string appName, 
            string versionName,
            string externalIp)
        {
            this.AppName = appName;
            this.VersionName = versionName;
            this.IpList = new[] { externalIp };
        }
        
        /// <summary>Parse to json str</summary>
        public override string ToString() =>
            JsonConvert.SerializeObject(this);
    }
}
