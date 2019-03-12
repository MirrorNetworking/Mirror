using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Assembly = System.Reflection.Assembly;

namespace Mirror.Weaver
{
    internal class ComplilationFinishedHook
    {
        [InitializeOnLoadMethod]
        static void OnInitializeOnLoad()
        {
            CompilationPipeline.assemblyCompilationFinished += OnCompilationFinished;
        }

        static string FindMirrorRuntime()
        {
            UnityEditor.Compilation.Assembly[] assemblies = CompilationPipeline.GetAssemblies();

            foreach (UnityEditor.Compilation.Assembly assembly in assemblies)
            {
                if (assembly.name == "Mirror")
                {
                    return assembly.outputPath;
                }
            }
            return "";
        }

        // TODO try our previous way:
        // UnityEditorInternal.InternalEditorUtility.GetEngineCoreModuleAssemblyPath()
        static string FindUnityEngineRuntime(Assembly[] assemblies)
        {
            foreach (Assembly assembly in assemblies)
            {
                try
                {
                    if (assembly.Location.Contains("UnityEngine.CoreModule"))
                    {
                        return assembly.Location;
                    }
                }
                catch (NotSupportedException)
                {
                    // in memory assembly, can't get location
                }
            }
            return "";
        }

        static bool CompilerMessagesContainError(CompilerMessage[] messages)
        {
            return messages.Any(msg => msg.type == CompilerMessageType.Error);
        }

        static void OnCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            const string k_HlapiRuntimeAssemblyName = "Mirror";

            // Do nothing if there were compile errors on the target
            if (CompilerMessagesContainError(messages))
            {
                Debug.Log("Weaver: stop because compile errors on target");
                return;
            }

            // Should not run on the editor only assemblies
            if (assemblyPath.Contains("-Editor") || assemblyPath.Contains(".Editor"))
            {
                return;
            }

            // don't weave mirror files
            string assemblyName = Path.GetFileName(assemblyPath);
            if (assemblyName == "Telepathy.dll" || assemblyName == "Mirror.dll" || assemblyName == "Mirror.Weaver.dll")
            {
                return;
            }

            string mirrorRuntimeDll = FindMirrorRuntime();
            if (string.IsNullOrEmpty(mirrorRuntimeDll))
            {
                Debug.LogError("Failed to find Mirror runtime assembly");
                return;
            }
            if (!File.Exists(mirrorRuntimeDll))
            {
                // this is normal, it happens with any assembly that is built before mirror
                // such as unity packages or your own assemblies
                // those don't need to be weaved
                // if any assembly depends on mirror, then it will be built after
                return;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            string unityEngine = FindUnityEngineRuntime(assemblies);
            if (string.IsNullOrEmpty(unityEngine))
            {
                Debug.LogError("Failed to find UnityEngine assembly");
                return;
            }

            bool usesMirror = false;
            bool foundThisAssembly = assemblies.Any(assembly => assembly.GetName().Name == Path.GetFileNameWithoutExtension(assemblyPath));
            HashSet<string> dependencyPaths = new HashSet<string>();
            foreach (Assembly assembly in assemblies)
            {
                // Find the assembly currently being compiled from domain assembly list and check if it's using Mirror
                if (assembly.GetName().Name == Path.GetFileNameWithoutExtension(assemblyPath))
                {
                    foreach (AssemblyName dependency in assembly.GetReferencedAssemblies())
                    {
                        // Since this assembly is already loaded in the domain this is a no-op and returns the
                        // already loaded assembly
                        string location = Assembly.Load(dependency).Location;
                        dependencyPaths.Add(Path.GetDirectoryName(location));
                        if (dependency.Name.Contains(k_HlapiRuntimeAssemblyName))
                        {
                            usesMirror = true;
                        }
                    }
                }
            }

            if (!foundThisAssembly)
            {
                // Target assembly not found in current domain, trying to load it to check references
                // will lead to trouble in the build pipeline, so lets assume it should go to weaver.
                // Add all assemblies in current domain to dependency list since there could be a
                // dependency lurking there (there might be generated assemblies so ignore file not found exceptions).
                // (can happen in runtime test framework on editor platform and when doing full library reimport)
                foreach (Assembly assembly in assemblies)
                {
                    try
                    {
                        if (!assembly.IsDynamic)
                            dependencyPaths.Add(Path.GetDirectoryName(Assembly.Load(assembly.GetName().Name).Location));
                    }
                    catch (FileNotFoundException) { }
                }
                usesMirror = true;
            }

            if (!usesMirror)
            {
                return;
            }

            string outputDirectory = Application.dataPath + "/../" + Path.GetDirectoryName(assemblyPath);

            Debug.Log("Weaving: " + assemblyPath + " unityengine=" + unityEngine + " mirrorRuntimeDll=" + mirrorRuntimeDll);
            if (Program.Process(unityEngine, mirrorRuntimeDll, outputDirectory, new[] { assemblyPath }, dependencyPaths.ToArray(), (value) => { Debug.LogWarning(value); }, (value) => { Debug.LogError(value); }))
            {
                Debug.Log("Weaving succeeded for: " + assemblyPath);
            }
            else
            {
                Debug.LogError("Weaving failed for: " + assemblyPath);
            }
        }
    }
}
