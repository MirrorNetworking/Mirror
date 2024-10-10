using System;
using System.IO;
using UnityEngine;

namespace Edgegap.Editor
{
    [Serializable]
    public class PackageJSON
    {
        [Serializable]
        public struct Author
        {
            public string name;
            public string email;
            public string url;
        }

        public string name;
        public string version;
        public string displayName;
        public string description;
        public string unity;
        public string unityRelease;
        public string documentationUrl;
        public string changelogUrl;
        public string licensesUrl;

        public Author author;

        // dependencies omitted since JsonUtility doesn't support dictionaries

        public static PackageJSON PackageJSONFromJSON(string path)
        {
            return JsonUtility.FromJson<PackageJSON>(File.ReadAllText(path));
        }
    }
}