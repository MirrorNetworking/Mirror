/*
    MIT License: The code in this script is mostly from https://github.com/MirageNet/unity-packer
        which is the source for the nuget unity-packer from https://www.nuget.org/packages/unity-packer
    Specifically the Pack method of the Packer class, related methods, plus the Utils.CreateGUID and
    Archive.AddFilesRecursive methods, adjusted for use in a .csx script called from a GitHub Action.

    Environment Variables are used instead of command line arguments to pass the output file name, assets, and dependencies.

    The AddDependenciesFile method is added to create a packagemanagermanifest asset file with
    Newtonsoft.Json and Unity Test Framework dependencies.
*/

#r "nuget: SharpZipLib, 1.4.2"
#r "nuget: YamlDotNet, 16.3.0"

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using YamlDotNet.RepresentationModel;

static StringSplitOptions stringSplitOptions = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;

enum CmdLineArgs : int { DotNetScriptDll, ScriptFullName, Version }
static string[] args = Environment.GetCommandLineArgs();

//for (int i = 0; i < args.Length; i++)
//    Console.WriteLine($"UnityPack: args[{i}]: {args[i]}");

// Output from GitHub Action logging for reference
// UnityPack: args[0]: C:\Users\runneradmin\.dotnet\tools\.store\dotnet-script\1.5.0\dotnet-script\1.5.0\tools\net8.0\any\dotnet-script.dll
// UnityPack: args[1]: .github/UnityPack.csx
// UnityPack: args[2]: 1.1.0

if (args.Length < 3)
{
    Console.WriteLine("Usage: UnityPack.csx <version>");
    return;
}

// Get version argument
static string version = args[(int)CmdLineArgs.Version];

// Get output file name
static string outputFile = Environment.GetEnvironmentVariable("UNITYPACK_OUTPUT") ?? "output.unitypackage";

if (!Path.IsPathRooted(outputFile))
    outputFile = Path.GetFullPath(outputFile);

Console.WriteLine($"UnityPack: outputFile:{outputFile} version:{version}");

// Create assets dictionary
static Dictionary<string, string> assets = new Dictionary<string, string>();
var assetVars = Environment.GetEnvironmentVariables()
    .Cast<System.Collections.DictionaryEntry>()
    .Where(e => e.Key.ToString().StartsWith("UNITYPACK_ASSET"))
    .ToDictionary(e => e.Key.ToString(), e => e.Value.ToString());

foreach (var kvp in assetVars)
{
    string[] parts = kvp.Value.Split(' ', stringSplitOptions);
    string source = parts[0];
    string destination = parts.Length > 1 ? parts[1] : "";
    assets[source] = destination;
}

// Create dependencies dictionary
static Dictionary<string, string> dependencies = new Dictionary<string, string>();
var envVars = Environment.GetEnvironmentVariables()
    .Cast<System.Collections.DictionaryEntry>()
    .Where(e => e.Key.ToString().StartsWith("UNITYPACK_DEPENDENCY"))
    .ToDictionary(e => e.Key.ToString(), e => e.Value.ToString());

foreach (var kvp in envVars)
{
    string[] parts = kvp.Value.Split(' ', stringSplitOptions);
    string name = parts[0];
    string value = parts.Length > 1 ? parts[1] : "";
    dependencies[name] = value;
}

// Create testables list
static List<string> testables = Environment.GetEnvironmentVariable("UNITYPACK_TESTABLES")
    ?.Split(' ', stringSplitOptions)
    .Where(t => !string.IsNullOrWhiteSpace(t))
    .ToList() ?? new List<string>();

Pack();

static void Pack()
{
    string tempPath = Path.Combine(Path.GetTempPath(), $"Mirror-{version}");
    Directory.CreateDirectory(tempPath);
    Console.WriteLine($"UnityPack: tempPath: {tempPath}");

    AddAssets(tempPath);

    AddDependenciesFile(tempPath);

    if (File.Exists(outputFile))
        File.Delete(outputFile);

    Compress(outputFile, tempPath);

    // Clean up
    Directory.Delete(tempPath, true);
}

static void AddAssets(string tempPath)
{
    foreach (KeyValuePair<string, string> fileEntry in assets)
    {
        if (File.Exists(fileEntry.Key))
            AddAsset(tempPath, fileEntry.Key, fileEntry.Value);
        else if (Directory.Exists(fileEntry.Key))
            AddFolder(tempPath, fileEntry.Key, fileEntry.Value);
        else
            throw new FileNotFoundException($"Could not find file or directory {fileEntry.Key}");
    }
}

static void AddFolder(string tempPath, string folder, string destination)
{
    Console.WriteLine($"UnityPack: Processing folder {folder}");

    string[] folders = Directory.GetDirectories(folder, "*", SearchOption.AllDirectories);
    string[] files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);

    var entries = new List<string>(folders);
    entries.AddRange(files);

    foreach (string filename in entries)
    {
        // metas will be copied with their asset
        if (Path.GetExtension(filename) == ".meta")
            continue;

        string destinationPath = Path.Combine(destination, Path.GetRelativePath(folder, filename));

        // unitypackage is always using / for directory separator
        destinationPath = destinationPath.Replace(Path.DirectorySeparatorChar, '/');

        AddAsset(tempPath, filename, destinationPath);
    }
}

static void AddAsset(string tempPath, string fromFile, string toPath)
{
    YamlDocument meta = GetMeta(fromFile) ?? GenerateMeta(fromFile, toPath);

    string guid = GetGuid(meta);

    Directory.CreateDirectory(Path.Combine(tempPath, guid));

    if (File.Exists(fromFile))
    {
        string assetPath = Path.Combine(tempPath, guid, "asset");
        File.Copy(fromFile, assetPath);
    }

    string pathnamePath = Path.Combine(tempPath, guid, "pathname");
    File.WriteAllText(pathnamePath, toPath);

    string metaPath = Path.Combine(tempPath, guid, "asset.meta");
    SaveMeta(metaPath, meta);
}

static YamlDocument GetMeta(string filename)
{
    // do we have a .meta file?
    string metaPath = filename + ".meta";

    if (!File.Exists(metaPath))
        return null;

    using var reader = new StreamReader(metaPath);
    var yaml = new YamlStream();
    yaml.Load(reader);

    return yaml.Documents[0];
}

static YamlDocument GenerateMeta(string fromFile, string toFile)
{
    string guid = CreateGuid(toFile);

    if (Directory.Exists(fromFile))
    {
        // this is a folder
        return new YamlDocument(new YamlMappingNode
            {
                {"guid", guid},
                {"fileFormatVersion", "2"},
                {"folderAsset", "yes"}
            });
    }
    else
    {
        // this is a file
        return new YamlDocument(new YamlMappingNode
            {
                {"guid", guid},
                {"fileFormatVersion", "2"}
            });
    }
}

static string GetGuid(YamlDocument meta)
{
    var mapping = (YamlMappingNode)meta.RootNode;
    var key = new YamlScalarNode("guid");
    var value = (YamlScalarNode)mapping[key];
    return value.Value;
}

static string CreateGuid(string input)
{
    using (MD5 md5 = MD5.Create())
    {
        byte[] inputBytes = Encoding.Unicode.GetBytes(input);
        byte[] hashBytes = md5.ComputeHash(inputBytes);

        StringBuilder stringBuilder = new StringBuilder();

        foreach (byte b in hashBytes)
            stringBuilder.Append(b.ToString("X2"));

        return stringBuilder.ToString();
    }
}

static void SaveMeta(string metaPath, YamlDocument meta)
{
    using (var writer = new StreamWriter(metaPath))
    {
        new YamlStream(meta).Save(writer, false);
    }

    var metaFile = new FileInfo(metaPath);

    using FileStream metaFileStream = metaFile.Open(FileMode.Open);
    metaFileStream.SetLength(metaFile.Length - 3 - Environment.NewLine.Length);
}

static void AddDependenciesFile(string tempPath)
{
    //string depenciesJson = "{\"dependencies\":{\"com.unity.nuget.newtonsoft-json\":\"3.0.0\"},\"testables\":[\"com.unity.test-framework.performance\"]}";

    // Serialize the JSON object
    var jsonObject = new
    {
        dependencies,
        testables
    };

    string depenciesJson = JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true });
    string depenciesPath = Path.Combine(tempPath, "packagemanagermanifest");
    Directory.CreateDirectory(depenciesPath);
    Console.WriteLine($"UnityPack: Creating dependency file at {Path.Combine(depenciesPath, "asset")}");
    File.WriteAllText(Path.Combine(depenciesPath, "asset"), depenciesJson);
}

static void Compress(string outputFile, string tempPath)
{
    Console.WriteLine($"UnityPack: Compressing from {tempPath} to {outputFile}");
    using var stream = new FileStream(outputFile, FileMode.CreateNew);
    using var zipStream = new GZipOutputStream(stream);
    using var archive = TarArchive.CreateOutputTarArchive(zipStream);
    archive.RootPath = tempPath.Replace('\\', '/');

    Console.WriteLine($"UnityPack: RootPath: {archive.RootPath}");

    AddFilesRecursive(archive, tempPath);
}

static void AddFilesRecursive(TarArchive archive, string tempPath)
{
    string[] files = Directory.GetFiles(tempPath, "*", SearchOption.AllDirectories);

    foreach (string filename in files)
    {
        var entry = TarEntry.CreateEntryFromFile(filename);
        if (archive.RootPath != null && Path.IsPathRooted(filename))
            entry.Name = Path.GetRelativePath(tempPath, filename);

        entry.Name = entry.Name.Replace('\\', '/');

        //Console.WriteLine($"UnityPack: Adding {filename} ({Path.IsPathRooted(filename)}) -> {entry.Name}");

        archive.WriteEntry(entry, true);
    }
}
