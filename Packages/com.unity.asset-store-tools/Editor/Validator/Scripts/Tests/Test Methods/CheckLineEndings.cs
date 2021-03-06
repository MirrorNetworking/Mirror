using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEditor;
using UnityObject = UnityEngine.Object;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class CheckLineEndings : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };

            var scripts = AssetUtility.GetObjectsFromAssets<MonoScript>(config.ValidationPaths, AssetType.MonoScript);

            var affectedScripts = new ConcurrentBag<UnityObject>();
            var scriptContents = new ConcurrentDictionary<MonoScript, string>();

            // A separate dictionary is needed because MonoScript contents cannot be accessed outside of the main thread
            foreach (var s in scripts)
                if (s != null)
                    scriptContents.TryAdd(s, s.text);

            Parallel.ForEach(scriptContents, (s) =>
            {
                if (HasInconsistentLineEndings(s.Value))
                    affectedScripts.Add(s.Key);
            });

            if (affectedScripts.Count > 0)
            {
                result.Result = TestResult.ResultStatus.VariableSeverityIssue;
                result.AddMessage("The following scripts have inconsistent line endings:", null, affectedScripts.ToArray());
            }
            else
            {
                result.Result = TestResult.ResultStatus.Pass;
                result.AddMessage("No scripts with inconsistent line endings were found!");
            }

            return result;
        }

        private bool HasInconsistentLineEndings(string text)
        {
            int crlfEndings = 0;
            int lfEndings = 0;

            var split = text.Split(new[] { "\n" }, StringSplitOptions.None);
            for (int i = 0; i < split.Length; i++)
            {
                var line = split[i];
                if (line.EndsWith("\r"))
                    crlfEndings++;
                else if (i != split.Length - 1)
                    lfEndings++;
            }

            if (crlfEndings > 0 && lfEndings > 0)
                return true;
            return false;
        }
    }
}
