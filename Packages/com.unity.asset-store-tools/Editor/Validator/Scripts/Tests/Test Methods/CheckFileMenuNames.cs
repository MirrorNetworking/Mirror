using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityObject = UnityEngine.Object;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class CheckFileMenuNames : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };

            var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            #region Scripts

            var scripts = AssetUtility.GetObjectsFromAssets<MonoScript>(config.ValidationPaths, AssetType.MonoScript).ToArray();
            var scriptTypes = ScriptUtility.GetTypesFromScriptAssets(scripts);
            var affectedScripts = new Dictionary<MonoScript, List<string>>();

            foreach (var kvp in scriptTypes)
            {
                var badMethods = new List<string>();
                foreach (var type in kvp.Value)
                {
                    foreach (var method in type.GetMethods(bindingFlags))
                    {
                        var attributes = method.GetCustomAttributes<MenuItem>().ToList();
                        if (attributes.Count == 0)
                            continue;

                        var badAttributes = attributes.Where(x => !IsValidMenuItem(x.menuItem)).ToList();
                        if (badAttributes.Count > 0)
                            badMethods.Add($"{string.Join("\n", badAttributes.Select(x => $"\'{x.menuItem}\'"))}\n(for method '{method.Name}')\n");
                    }
                }

                if (badMethods.Count > 0)
                    affectedScripts.Add(kvp.Key, badMethods);
            }

            #endregion

            #region Precompiled Assemblies

            var assemblies = AssetUtility.GetObjectsFromAssets(config.ValidationPaths, AssetType.PrecompiledAssembly).ToArray();
            var assemblyTypes = ScriptUtility.GetTypesFromAssemblies(assemblies);
            var affectedAssemblies = new Dictionary<UnityObject, List<string>>();

            foreach (var kvp in assemblyTypes)
            {
                var badMethods = new List<string>();
                foreach (var type in kvp.Value)
                {
                    foreach (var method in type.GetMethods(bindingFlags))
                    {
                        var attributes = method.GetCustomAttributes<MenuItem>().ToList();
                        if (attributes.Count == 0)
                            continue;

                        var badAttributes = attributes.Where(x => !IsValidMenuItem(x.menuItem)).ToList();
                        if (badAttributes.Count > 0)
                            badMethods.Add($"{string.Join("\n", badAttributes.Select(x => (x as MenuItem).menuItem))}\n(Method '{method.Name}')\n");
                    }
                }

                if (badMethods.Count > 0)
                    affectedAssemblies.Add(kvp.Key, badMethods);
            }

            #endregion

            if (affectedScripts.Count > 0 || affectedAssemblies.Count > 0)
            {
                if (affectedScripts.Count > 0)
                {
                    result.Result = TestResult.ResultStatus.VariableSeverityIssue;
                    result.AddMessage("The following scripts contain invalid MenuItem names:");
                    foreach (var kvp in affectedScripts)
                    {
                        var message = string.Empty;
                        foreach (var type in kvp.Value)
                            message += type + "\n";

                        message = message.Remove(message.Length - "\n".Length);
                        result.AddMessage(message, null, kvp.Key);
                    }
                }

                if (affectedAssemblies.Count > 0)
                {
                    result.Result = TestResult.ResultStatus.VariableSeverityIssue;
                    result.AddMessage("The following assemblies contain invalid MenuItem names:");
                    foreach (var kvp in affectedAssemblies)
                    {
                        var message = string.Empty;
                        foreach (var type in kvp.Value)
                            message += type + "\n";

                        message = message.Remove(message.Length - "\n".Length);
                        result.AddMessage(message, null, kvp.Key);
                    }
                }
            }
            else
            {
                result.Result = TestResult.ResultStatus.Pass;
                result.AddMessage("No MenuItems with invalid names were found!");
            }

            return result;
        }

        private bool IsValidMenuItem(string menuItemName)
        {
            var acceptableMenuItems = new string[]
            {
                "File",
                "Edit",
                "Assets",
                "GameObject",
                "Component",
                "Window",
                "Help",
                "CONTEXT",
                "Tools"
            };

            menuItemName = menuItemName.Replace("\\", "/");
            if (acceptableMenuItems.Any(x => menuItemName.ToLower().StartsWith($"{x.ToLower()}/")))
                return true;

            return false;
        }
    }
}
