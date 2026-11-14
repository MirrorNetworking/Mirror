using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

namespace Edgegap.Editor
{
    [Serializable]
    public class GithubRelease
    {
        public string name;

        public static async Task<string> GithubReleaseFromAPI()
        {
            HttpClient http = new HttpClient();
            http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            http.DefaultRequestHeaders.Add("User-Agent", "Unity");
            http.Timeout = TimeSpan.FromSeconds(30);

            HttpResponseMessage response = await http.GetAsync("https://api.github.com/repos/edgegap/edgegap-unity-plugin/releases/latest").ConfigureAwait(false);

            return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync() : "{}";
        }

        public static GithubRelease GithubReleaseFromJSON(string json)
        {
            return JsonUtility.FromJson<GithubRelease>(json);
        }
    }
}