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
        const string MirrorRuntimeAssemblyName = "Mirror";
        const string MirrorWeaverAssemblyName = "Mirror.Weaver";

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
                if (assembly.name == MirrorRuntimeAssemblyName)
                {
                    return assembly.outputPath;
                }
            }
            return "";
        }

        // get all dependency directories
        static HashSet<string> GetDependencyDirectories(AssemblyName[] dependencies)
        {
            // Since this assembly is already loaded in the domain this is a
            // no-op and returns the already loaded assembly
            return new HashSet<string>(
                dependencies.Select(dependency => Path.GetDirectoryName(Assembly.Load(dependency).Location))
            );
        }

        // get all non-dynamic assembly directories
        static HashSet<string> GetNonDynamicAssemblyDirectories(Assembly[] assemblies)
        {
            HashSet<string> paths = new HashSet<string>();

            foreach (Assembly assembly in assemblies)
            {
                if (!assembly.IsDynamic)
                {
                    // need to check if file exists to avoid potential
                    // FileNotFoundException in Assembly.Load
                    string assemblyName = assembly.GetName().Name;
                    if (File.Exists(assemblyName))
                    {
                        paths.Add(Path.GetDirectoryName(Assembly.Load(assemblyName).Location));
                    }
                }
            }

            return paths;
        }

        static bool CompilerMessagesContainError(CompilerMessage[] messages)
        {
            return messages.Any(msg => msg.type == CompilerMessageType.Error);
        }

        static void OnCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
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
            string assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
            if (assemblyName == MirrorRuntimeAssemblyName || assemblyName == MirrorWeaverAssemblyName)
            {
                return;
            }

            // find Mirror.dll
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

            // find UnityEngine.CoreModule.dll
            string unityEngineCoreModuleDLL = UnityEditorInternal.InternalEditorUtility.GetEngineCoreModuleAssemblyPath();
            if (string.IsNullOrEmpty(unityEngineCoreModuleDLL))
            {
                Debug.LogError("Failed to find UnityEngine assembly");
                return;
            }

            // find all assemblies and the currently compiling assembly
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            Assembly targetAssembly = assemblies.FirstOrDefault(asm => asm.GetName().Name == Path.GetFileNameWithoutExtension(assemblyPath));

            // prepare variables
            HashSet<string> dependencyPaths = new HashSet<string>();

            // found this assembly in assemblies?
            if (targetAssembly != null)
            {
                // get all dependencies for the target assembly
                AssemblyName[] dependencies = targetAssembly.GetReferencedAssemblies();

                // does the target assembly depend on Mirror at all?
                // otherwise there is nothing to weave anyway.
                // TODO don't use contains
                bool usesMirror = dependencies.Any(dependency => dependency.Name.Contains(MirrorRuntimeAssemblyName));
                if (!usesMirror)
                {
                    return;
                }

                // get all the directories
                dependencyPaths = GetDependencyDirectories(dependencies);
            }
            else
            {
                // Target assembly not found in current domain, trying to load it to check references
                // will lead to trouble in the build pipeline, so lets assume it should go to weaver.
                // Add all assemblies in current domain to dependency list since there could be a
                // dependency lurking there (there might be generated assemblies so ignore file not found exceptions).
                // (can happen in runtime test framework on editor platform and when doing full library reimport)
                dependencyPaths = GetNonDynamicAssemblyDirectories(assemblies);
            }

            // construct full path to Project/Library/ScriptAssemblies
            string projectDirectory = Directory.GetParent(Application.dataPath).ToString();
            string outputDirectory = Path.Combine(projectDirectory, Path.GetDirectoryName(assemblyPath));

            Debug.Log("Weaving: " + assemblyPath + " unityengine=" + unityEngineCoreModuleDLL + " mirrorRuntimeDll=" + mirrorRuntimeDll);
            if (Program.Process(unityEngineCoreModuleDLL, mirrorRuntimeDll, outputDirectory, new[] { assemblyPath }, dependencyPaths.ToArray(), (value) => { Debug.LogWarning(value); }, (value) => { Debug.LogError(value); }))
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
