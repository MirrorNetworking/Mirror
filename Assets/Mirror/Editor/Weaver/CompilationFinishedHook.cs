using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        static bool CompilerMessagesContainError(CompilerMessage[] messages)
        {
            return messages.Any(msg => msg.type == CompilerMessageType.Error);
        }

        static void OnCompilationFinished(string targetAssembly, CompilerMessage[] messages)
        {
            const string k_HlapiRuntimeAssemblyName = "Mirror";

            // Do nothing if there were compile errors on the target
            if (CompilerMessagesContainError(messages))
            {
                Debug.Log("Weaver: stop because compile errors on target");
                return;
            }

            // Should not run on the editor only assemblies
            if (targetAssembly.Contains("-Editor") || targetAssembly.Contains(".Editor"))
            {
                return;
            }

            // Should not run on own assembly
            if (targetAssembly.Contains(k_HlapiRuntimeAssemblyName))
            {
                return;
            }


            string mirrorRuntimeDll = FindMirrorRuntime();
            if (!File.Exists(mirrorRuntimeDll))
            {
                // this is normal, it happens with any assembly that is built before mirror
                // such as unity packages or your own assemblies
                // those don't need to be weaved
                // if any assembly depends on mirror, then it will be built after
                return;
            }

            var scriptAssembliesPath = Application.dataPath + "/../" + Path.GetDirectoryName(targetAssembly);
            string unityEngine = "";
            var outputDirectory = scriptAssembliesPath;
            var assemblyPath = targetAssembly;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            bool usesUnet = false;
            bool foundThisAssembly = false;
            HashSet<string> dependencyPaths = new HashSet<string>();
            foreach (var assembly in assemblies)
            {
                // Find the assembly currently being compiled from domain assembly list and check if it's using unet
                if (assembly.GetName().Name == Path.GetFileNameWithoutExtension(targetAssembly))
                {
                    foundThisAssembly = true;
                    foreach (var dependency in assembly.GetReferencedAssemblies())
                    {
                        // Since this assembly is already loaded in the domain this is a no-op and retuns the
                        // already loaded assembly
                        var location = Assembly.Load(dependency).Location;
                        dependencyPaths.Add(Path.GetDirectoryName(location));
                        if (dependency.Name.Contains(k_HlapiRuntimeAssemblyName))
                        {
                            usesUnet = true;
                        }
                    }
                }
                try
                {
                    if (assembly.Location.Contains("UnityEngine.CoreModule"))
                    {
                        unityEngine = assembly.Location;
                    }
                    /*if (assembly.Location.Contains(k_HlapiRuntimeAssemblyName))
                    {
                        unetAssemblyPath = assembly.Location;
                    }*/
                }
                catch (NotSupportedException)
                {
                    // in memory assembly, can't get location
                }
            }

            if (!foundThisAssembly)
            {
                // Target assembly not found in current domain, trying to load it to check references
                // will lead to trouble in the build pipeline, so lets assume it should go to weaver.
                // Add all assemblies in current domain to dependency list since there could be a
                // dependency lurking there (there might be generated assemblies so ignore file not found exceptions).
                // (can happen in runtime test framework on editor platform and when doing full library reimport)
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        if (!assembly.IsDynamic)
                            dependencyPaths.Add(Path.GetDirectoryName(Assembly.Load(assembly.GetName().Name).Location));
                    }
                    catch (FileNotFoundException) { }
                }
                usesUnet = true;
            }

            if (!usesUnet)
            {
                return;
            }

            if (string.IsNullOrEmpty(unityEngine))
            {
                Debug.LogError("Failed to find UnityEngine assembly");
                return;
            }

            if (string.IsNullOrEmpty(mirrorRuntimeDll))
            {
                Debug.LogError("Failed to find Mirror runtime assembly");
                return;
            }

            Debug.Log("Weaving: " + assemblyPath + " unityengine=" + unityEngine + " unetassembly=" + mirrorRuntimeDll);
            bool result = Program.Process(unityEngine, mirrorRuntimeDll, outputDirectory, new[] { assemblyPath }, dependencyPaths.ToArray(), (value) => { Debug.LogWarning(value); }, (value) => { Debug.LogError(value); });
            Debug.Log("Weaver result: " + result);
        }
    }
}
