using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mirror.Tests.Generators
{
    public class GeneratorBase
    {
        protected const string BaseNameSpace = "Mirror.Tests.Generated";

        protected static void Save(string main, string fileName, bool compile = true)
        {
            string filePath = compile
                ? $"Mirror/Tests/Editor/Generated/{fileName}.gen.cs"
                : $"Mirror/Tests/Editor/Generated/.{fileName}.gen.cs";
            File.WriteAllText($"{Application.dataPath}/{filePath}", main);
            if (compile)
            {
                AssetDatabase.Refresh();
                AssetDatabase.ImportAsset($"Assets/{filePath}");
            }
        }

        protected static string Merge(IEnumerable<string> strs, string separator = "\n")
        {
            return string.Join(separator, strs);
        }
    }
}
