using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.CecilX;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityAssembly = UnityEditor.Compilation.Assembly;

namespace Mirror.Weaver
{
    public static class CompilationFinishedHook
    {
        // needs to be the same as Weaver.MirrorAssemblyName!
        const string MirrorRuntimeAssemblyName = "Mirror";
        const string MirrorWeaverAssemblyName = "Mirror.Weaver";

        // global weaver define so that tests can use it
        internal static Weaver weaver;

        // delegate for subscription to Weaver warning messages
        public static Action<string> OnWeaverWarning;
        // delete for subscription to Weaver error messages
        public static Action<string> OnWeaverError;

        // controls weather Weaver errors are reported direct to the Unity console (tests enable this)
        public static bool UnityLogEnabled = true;

        [InitializeOnLoadMethod]
        public static void OnInitializeOnLoad()
        {
            CompilationPipeline.assemblyCompilationFinished += OnCompilationFinished;

            // We only need to run this once per session
            // after that, all assemblies will be weaved by the event
            if (!SessionState.GetBool("MIRROR_WEAVED", false))
            {
                // reset session flag
                SessionState.SetBool("MIRROR_WEAVED", true);
                SessionState.SetBool("MIRROR_WEAVE_SUCCESS", true);

                WeaveExistingAssemblies();
            }
        }

        public static void WeaveExistingAssemblies()
        {
            foreach (UnityAssembly assembly in CompilationPipeline.GetAssemblies())
            {
                if (File.Exists(assembly.outputPath))
                {
                    OnCompilationFinished(assembly.outputPath, new CompilerMessage[0]);
                }
            }

#if UNITY_2019_3_OR_NEWER
            EditorUtility.RequestScriptReload();
#else
            UnityEditorInternal.InternalEditorUtility.RequestScriptReload();
#endif
        }

        static Assembly FindMirrorRuntime() =>
            CompilationPipeline.GetAssemblies().First(assembly => assembly.name == MirrorRuntimeAssemblyName);

        static bool CompilerMessagesContainError(CompilerMessage[] messages) =>
            messages.Any(msg => msg.type == CompilerMessageType.Error);

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
            Assembly mirrorAssembly = FindMirrorRuntime();
            if (mirrorAssembly == null)
            {
                Debug.LogError("Failed to find Mirror runtime assembly");
                return;
            }

            string mirrorRuntimeDll = mirrorAssembly.outputPath;
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

            HashSet<string> dependencyPaths = GetDependecyPaths(assemblyPath);
            dependencyPaths.Add(Path.GetDirectoryName(mirrorRuntimeDll));
            dependencyPaths.Add(Path.GetDirectoryName(unityEngineCoreModuleDLL));

            if (!WeaveFromFile(assemblyPath, dependencyPaths.ToArray()))
            {
                // Set false...will be checked in \Editor\EnterPlayModeSettingsCheck.CheckSuccessfulWeave()
                SessionState.SetBool("MIRROR_WEAVE_SUCCESS", false);
                if (UnityLogEnabled) Debug.LogError("Weaving failed for: " + assemblyPath);
            }
        }

        static HashSet<string> GetDependecyPaths(string assemblyPath)
        {
            // build directory list for later asm/symbol resolving using CompilationPipeline refs
            HashSet<string> dependencyPaths = new HashSet<string>
            {
                Path.GetDirectoryName(assemblyPath)
            };
            foreach (UnityAssembly unityAsm in CompilationPipeline.GetAssemblies())
            {
                if (unityAsm.outputPath == assemblyPath)
                {
                    foreach (string unityAsmRef in unityAsm.compiledAssemblyReferences)
                    {
                        dependencyPaths.Add(Path.GetDirectoryName(unityAsmRef));
                    }
                }
            }

            return dependencyPaths;
        }
        // helper function to invoke Weaver with an AssemblyDefinition from a
        // file path, with dependencies added.
        static bool WeaveFromFile(string assemblyPath, string[] dependencies)
        {
            // open the assembly file as stream
            using (FileStream stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.ReadWrite))
            // resolve assembly from stream
            using (DefaultAssemblyResolver asmResolver = new DefaultAssemblyResolver())
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(stream, new ReaderParameters{ ReadWrite = true, ReadSymbols = true, AssemblyResolver = asmResolver }))
            {
                // add this assembly's path and unity's assembly path
                asmResolver.AddSearchDirectory(Path.GetDirectoryName(assemblyPath));
                asmResolver.AddSearchDirectory(Helpers.UnityEngineDllDirectoryName());

                // add dependencies
                if (dependencies != null)
                {
                    foreach (string path in dependencies)
                    {
                        asmResolver.AddSearchDirectory(path);
                    }
                }

                // create weaver with logger
                weaver = new Weaver(new CompilationFinishedLogger());
                if (weaver.Weave(assembly, asmResolver, out bool modified))
                {
                    // write changes to file if modified
                    if (modified)
                        assembly.Write(new WriterParameters{WriteSymbols = true});

                    return true;
                }
                return false;
            }
        }
    }
}
