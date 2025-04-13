#!/usr/bin/env dotnet-script
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;

const string LOGIN_URL = "https://api.assetstore.unity3d.com/publisher/v1/session"; // From Constants.Api.SessionUrl
const string UPLOAD_URL = "https://api.assetstore.unity3d.com/publisher/v1/package/upload"; // From Constants.Api.UploadUnityPackageUrl
const string LOG_PREFIX = "UnityPublish: ";

var username = Environment.GetEnvironmentVariable("UNITY_USERNAME");
var password = Environment.GetEnvironmentVariable("UNITY_PASSWORD");
var packagePath = Environment.GetEnvironmentVariable("PACKAGE_PATH");
var licenseHash = Environment.GetEnvironmentVariable("PUBLISH_LICENSE_HASH");
var hardwareHash = Environment.GetEnvironmentVariable("PUBLISH_HARDWARE_HASH");
var version = Environment.GetEnvironmentVariable("RELEASE_VERSION");

if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(packagePath))
{
    Console.WriteLine($"{LOG_PREFIX}Missing required environment variables (username, password, or package path).");
    Environment.Exit(1);
}
if (string.IsNullOrEmpty(licenseHash) || string.IsNullOrEmpty(hardwareHash))
{
    Console.WriteLine($"{LOG_PREFIX}Missing required environment variables (license_hash or hardware_hash).");
    Environment.Exit(1);
}
if (string.IsNullOrEmpty(version))
{
    Console.WriteLine($"{LOG_PREFIX}Missing required environment variable (RELEASE_VERSION).");
    Environment.Exit(1);
}

using (var client = new HttpClient())
{
    client.DefaultRequestHeaders.ConnectionClose = false;
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    await Main(client);
}

async Task Main(HttpClient client)
{
    Console.WriteLine($"{LOG_PREFIX}Starting Unity publishing process...");

    // Step 1: Login
    Console.WriteLine($"{LOG_PREFIX}Attempting login to Unity Publisher...");
    string sessionId = await LoginAsync(client, username, password, licenseHash, hardwareHash);
    if (string.IsNullOrEmpty(sessionId))
    {
        Console.WriteLine($"{LOG_PREFIX}Login failed. Aborting.");
        Environment.Exit(1);
    }
    Console.WriteLine($"{LOG_PREFIX}Login successful: {sessionId}");

    // Set session ID in headers (mimicking SetSessionId)
    client.DefaultRequestHeaders.Add("X-Unity-Session", sessionId);

    // Step 2: Upload Package
    //Console.WriteLine($"{LOG_PREFIX}Uploading package: {packagePath} (version: {version})");
    //bool uploadSuccess = await UploadPackageAsync(client, packagePath, version);
    //if (uploadSuccess)
    //{
    //    Console.WriteLine($"{LOG_PREFIX}Package uploaded successfully!");
    //}
    //else
    //{
    //    Console.WriteLine($"{LOG_PREFIX}Upload failed.");
    //    Environment.Exit(1);
    //}
}

async Task<string> LoginAsync(HttpClient client, string user, string pass, string license, string hardware)
{
    try
    {
        var loginPayload = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("username", user),
            new KeyValuePair<string, string>("password", pass),
            new KeyValuePair<string, string>("license_hash", license),
            new KeyValuePair<string, string>("hardware_hash", hardware)
        });
        var response = await client.PostAsync(LOGIN_URL, loginPayload);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"{LOG_PREFIX}Login request failed with status: {response.StatusCode}");
            Console.WriteLine($"-- {errorContent}");
            return null;
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"{LOG_PREFIX}Login response: {responseContent}");

        var json = JsonSerializer.Deserialize<Dictionary<string, string>>(responseContent);
        return json.TryGetValue("sessionId", out var sessionId) ? sessionId : null; // Updated to sessionId
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{LOG_PREFIX}Login error: {ex.Message}");
        return null;
    }
}

//async Task<bool> UploadPackageAsync(HttpClient client, string packagePath, string version)
//{
//    try
//    {
//        using var fileStream = File.OpenRead(packagePath);
//        using var content = new MultipartFormDataContent();
//        content.Add(new StreamContent(fileStream), "file", Path.GetFileName(packagePath));
//        content.Add(new StringContent(version), "version");

//        var response = await client.PostAsync(UPLOAD_URL, content);
//        if (response.IsSuccessStatusCode)
//        {
//            return true;
//        }
//        else
//        {
//            Console.WriteLine($"{LOG_PREFIX}Upload failed with status: {response.StatusCode}");
//            return false;
//        }
//    }
//    catch (Exception ex)
//    {
//        Console.WriteLine($"{LOG_PREFIX}Upload error: {ex.Message}");
//        return false;
//    }
//}
