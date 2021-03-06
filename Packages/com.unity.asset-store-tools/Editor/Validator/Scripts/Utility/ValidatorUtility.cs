using AssetStoreTools.Validator.TestDefinitions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace AssetStoreTools.Validator.Utility
{
    internal static class ValidatorUtility
    {
        private const string TestsPath = "Packages/com.unity.asset-store-tools/Editor/Validator/Scripts/Tests";
        private const string TestMethodsPath = "Packages/com.unity.asset-store-tools/Editor/Validator/Scripts/Tests/Test Methods";

        public enum SortType
        {
            Id,
            Alphabetical
        }

        public static ValidationTestScriptableObject[] GetAutomatedTestCases() => GetAutomatedTestCases(SortType.Id);

        public static ValidationTestScriptableObject[] GetAutomatedTestCases(SortType sortType)
        {
            string[] guids = AssetDatabase.FindAssets("t:AutomatedTestScriptableObject", new[] { TestsPath });
            ValidationTestScriptableObject[] tests = new ValidationTestScriptableObject[guids.Length];
            for (int i = 0; i < tests.Length; i++)
            {
                string testPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                AutomatedTestScriptableObject test = AssetDatabase.LoadAssetAtPath<AutomatedTestScriptableObject>(testPath);

                tests[i] = test;
            }

            switch (sortType)
            {
                default:
                case SortType.Id:
                    tests = tests.OrderBy(x => x.Id).ToArray();
                    break;
                case SortType.Alphabetical:
                    tests = tests.OrderBy(x => x.Title).ToArray();
                    break;
            }                

            return tests;
        }

        public static MonoScript GenerateTestScript(string testName)
        {
            var newScriptPath = $"{TestMethodsPath}/{testName}";
            if (!newScriptPath.EndsWith(".cs"))
                newScriptPath += ".cs";

            var existingScript = AssetDatabase.LoadAssetAtPath<MonoScript>(newScriptPath);
            if (existingScript != null)
                return existingScript;

            var scriptContent =
                $"using AssetStoreTools.Validator.Data;\n" +
                $"using AssetStoreTools.Validator.TestDefinitions;\n" +
                $"using AssetStoreTools.Validator.TestMethods.Utility;\n\n" +
                $"namespace AssetStoreTools.Validator.TestMethods\n" +
                "{\n" +
                $"    internal class {testName} : {nameof(ITestScript)}\n" +
                "    {\n" +
                $"        public TestResult Run({nameof(ValidationTestConfig)} config)\n" +
                "        {\n" +
                "            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };\n" +
                "            return result;\n" +
                "        }\n" +
                "    }\n" +
                "}\n";

            File.WriteAllText(newScriptPath, scriptContent);
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<MonoScript>(newScriptPath);
        }

        public static string GetLongestProjectPath()
        {
            var longPaths = GetProjectPaths(new string[] { "Assets", "Packages" });
            return longPaths.Aggregate("", (max, cur) => max.Length > cur.Length ? max : cur);
        }

        public static IEnumerable<string> GetProjectPaths(string[] rootPaths)
        {
            var longPaths = new List<string>();
            var guids = AssetDatabase.FindAssets("*", rootPaths);

            foreach(var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                longPaths.Add(path);
            }

            return longPaths;
        }
    }
}