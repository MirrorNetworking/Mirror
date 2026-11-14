using Newtonsoft.Json;
using System.Collections.Generic;

namespace Edgegap.Editor.Api.Models.Results
{
    /// <summary>
    /// Result model for `[GET] v1/app/{app_name}/versions`.
    /// GET API Doc | https://docs.edgegap.com/api/#tag/Applications/operation/app-versions-get
    /// </summary>
    public class GetAppVersionsResult
    {
        [JsonProperty("versions")]
        public List<VersionData> Versions { get; set; }
    }
}
