using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models
{
    public class LocationData
    {
        [JsonProperty("city")]
        public string City { get; set; }
            
        [JsonProperty("country")]
        public string Country { get; set; }
            
        [JsonProperty("continent")]
        public string Continent { get; set; }
            
        [JsonProperty("administrative_division")]
        public string AdministrativeDivision { get; set; }
            
        [JsonProperty("timezone")]
        public string Timezone { get; set; }
            
        [JsonProperty("latitude")]
        public double Latitude { get; set; }
            
        [JsonProperty("longitude")]
        public double Longitude { get; set; }
    }
}
