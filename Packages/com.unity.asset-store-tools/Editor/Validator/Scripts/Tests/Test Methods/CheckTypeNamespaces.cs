using AssetStoreTools.Validator.Data;
using AssetStoreTools.Validator.TestDefinitions;
using AssetStoreTools.Validator.TestMethods.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityObject = UnityEngine.Object;

namespace AssetStoreTools.Validator.TestMethods
{
    internal class CheckTypeNamespaces : ITestScript
    {
        public TestResult Run(ValidationTestConfig config)
        {
            var result = new TestResult() { Result = TestResult.ResultStatus.Undefined };

            #region Scripts

            var scripts = AssetUtility.GetObjectsFromAssets<MonoScript>(config.ValidationPaths, AssetType.MonoScript).ToArray();
            var scriptNamespaces = ScriptUtility.GetTypeNamespacesFromScriptAssets(scripts);
            var affectedScripts = new Dictionary<MonoScript, List<string>>();

            foreach (var kvp in scriptNamespaces)
            {
                var typesWithoutNamespace = new List<string>();
                foreach (var t in kvp.Value)
                {
                    if (string.IsNullOrEmpty(t.Namespace))
                        typesWithoutNamespace.Add(t.Name);
                }

                if (typesWithoutNamespace.Count > 0)
                    affectedScripts.Add(kvp.Key, typesWithoutNamespace);
            }

            #endregion

            #region Precompiled Assemblies

            var assemblies = AssetUtility.GetObjectsFromAssets(config.ValidationPaths, AssetType.PrecompiledAssembly).ToList();
            var assemblyTypes = ScriptUtility.GetTypesFromAssemblies(assemblies);
            var affectedAssemblies = new Dictionary<UnityObject, List<string>>();

            foreach (var kvp in assemblyTypes)
            {
                var typesWithoutNamespace = new List<string>();
                foreach (var t in kvp.Value)
                {
                    if (string.IsNullOrEmpty(t.Namespace))
                        typesWithoutNamespace.Add($"{GetTypeName(t)} {t.Name}");
                }

                if (typesWithoutNamespace.Count > 0)
                    affectedAssemblies.Add(kvp.Key, typesWithoutNamespace);
            }

            #endregion

            if (affectedScripts.Count > 0 || affectedAssemblies.Count > 0)
            {
                if (affectedScripts.Count > 0)
                {
                    result.Result = TestResult.ResultStatus.VariableSeverityIssue;
                    result.AddMessage("The following scripts contain types (classes, interfaces, structs or enums) not nested in a namespace:");
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
                    result.AddMessage("The following precompiled assemblies contain types not nested in a namespace:");
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
                result.AddMessage("No types without namespaces were found!");
            }

            return result;
        }

        private string GetTypeName(Type type)
        {
            if (type.IsClass)
                return "class";
            if (type.IsInterface)
                return "interface";
            if (type.IsEnum)
                return "enum";
            if (type.IsValueType)
                return "struct";

            throw new ArgumentException($"Received an unrecognizable type {type}. Type must be either a class, interface, struct or enum");
        }
    }
}
