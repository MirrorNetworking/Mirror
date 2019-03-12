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
    public class CompilationFinishedHook
    {
        const string MirrorRuntimeAssemblyName = "Mirror";
        const string MirrorWeaverAssemblyName = "Mirror.Weaver";

        public static Action<string> OnWeaverMessage; // delegate for subscription to Weaver debug messages
        public static Action<string> OnWeaverWarning; // delegate for subscription to Weaver warning messages
        public static Action<string> OnWeaverError; // delete for subscription to Weaver error messages

        public static bool WeaverEnabled { get; set; } // controls whether we weave any assemblies when CompilationPipeline delegates are invoked
        public static bool UnityLogEnabled = true; // controls weather Weaver errors are reported direct to the Unity console (tests enable this)
        public static bool WeaveFailed { get; private set; } // holds the result status of our latest Weave operation

        // debug message handler that also calls OnMessageMethod delegate
        static void HandleMessage(string msg)
        {
            if (UnityLogEnabled) Debug.Log(msg);
            if (OnWeaverMessage != null) OnWeaverMessage.Invoke(msg);
        }

        // warning message handler that also calls OnWarningMethod delegate
        static void HandleWarning(string msg)
        {
            if (UnityLogEnabled) Debug.LogWarning(msg);
            if (OnWeaverWarning != null) OnWeaverWarning.Invoke(msg);
        }

        // error message handler that also calls OnErrorMethod delegate
        static void HandleError(string msg)
        {
            if (UnityLogEnabled) Debug.LogError(msg);
            if (OnWeaverError != null) OnWeaverError.Invoke(msg);
        }

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
                bool usesMirror = dependencies.Any(dependency => dependency.Name == MirrorRuntimeAssemblyName);
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

            //if (UnityLogEnabled) Debug.Log("Weaving: " + assemblyPath); // uncomment to easily observe weave targets
            if (Program.Process(unityEngineCoreModuleDLL, mirrorRuntimeDll, outputDirectory, new[] { assemblyPath }, dependencyPaths.ToArray(), HandleWarning, HandleError))
            {
                WeaveFailed = false;
                Debug.Log("Weaving succeeded for: " + assemblyPath);
            }
            else
            {
                WeaveFailed = true;
                if (UnityLogEnabled) Debug.LogError("Weaving failed for: " + assemblyPath);
            }
        }
    }
}
