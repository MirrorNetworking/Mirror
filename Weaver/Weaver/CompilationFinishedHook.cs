// https://docs.unity3d.com/Manual/RunningEditorCodeOnLaunch.html
using System.IO;
using Mono.Cecil;
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System;
using System.Linq;

namespace Mirror.Weaver
{
    // InitializeOnLoad is needed for Unity to call the static constructor on load
    [InitializeOnLoad]
    public class CompilationFinishedHook
    {
        public static Action<string> OnWeaverMessage; // delegate for subscription to Weaver debug messages
        public static Action<string> OnWeaverWarning; // delegate for subscription to Weaver warning messages
        public static Action<string> OnWeaverError; // delete for subscription to Weaver error messages

        public static bool WeaverEnabled { get; set; } // controls whether we weave any assemblies when CompilationPipeline delegates are invoked
        public static bool UnityLogEnabled { get; set; } // controls weather Weaver errors are reported direct to the Unity console (tests enable this)
        public static bool WeaveFailed { get; private set; } // holds the result status of our latest Weave operation

        // debug message handler that also calls OnMessageMethod delegate
        static void HandleMessage(string msg)
        {
            if (UnityLogEnabled) Debug.Log(msg);
            OnWeaverMessage?.Invoke(msg);
        }

        // warning message handler that also calls OnWarningMethod delegate
        static void HandleWarning(string msg)
        {
            if (UnityLogEnabled) Debug.LogWarning(msg);
            OnWeaverWarning?.Invoke(msg);
        }

        // error message handler that also calls OnErrorMethod delegate
        static void HandleError(string msg)
        {
            if (UnityLogEnabled) Debug.LogError(msg);
            OnWeaverError?.Invoke(msg);
        }

        static CompilationFinishedHook()
        {
            // assemblyPath: Library/ScriptAssemblies/Assembly-CSharp.dll/
            // assemblyPath: Library/ScriptAssemblies/Assembly-CSharp-Editor.dll
            CompilationPipeline.assemblyCompilationFinished += (assemblyPath, messages) =>
            {
                // if user scripts can't be compiled because of errors,
                // assemblyCompilationFinished is still called but assemblyPath
                // file won't exist. in that case, do nothing.
                if (!File.Exists(assemblyPath))
                {
                    Console.WriteLine("Weaving skipped because assembly doesnt exist: " + assemblyPath);
                    return;
                }

                string assemblyName = Path.GetFileName(assemblyPath);

                if (assemblyName == "Telepathy.dll" || assemblyName == "Mirror.dll" || assemblyName == "Mirror.Weaver.dll")
                {
                    // don't weave mirror files
                    return;
                }

                // UnityEngineCoreModule.DLL path:
                string unityEngineCoreModuleDLL = UnityEditorInternal.InternalEditorUtility.GetEngineCoreModuleAssemblyPath();

                // outputDirectory is the directory of assemblyPath
                string outputDirectory = Path.GetDirectoryName(assemblyPath);

                string mirrorRuntimeDll = FindMirrorRuntime();
                if (!File.Exists(mirrorRuntimeDll))
                {
                    // this is normal, it happens with any assembly that is built before mirror
                    // such as unity packages or your own assemblies
                    // those don't need to be weaved
                    // if any assembly depends on mirror, then it will be built after
                    return;
                }

                // unity calls it for Library/ScriptAssemblies/Assembly-CSharp-Editor.dll too, but we don't want to (and can't) weave this one
                bool buildingForEditor = assemblyPath.EndsWith("Editor.dll");
                if (!buildingForEditor)
                {
                    if (UnityLogEnabled) Console.WriteLine("Weaving: " + assemblyPath);
                    // assemblyResolver: unity uses this by default:
                    //   ICompilationExtension compilationExtension = GetCompilationExtension();
                    //   IAssemblyResolver assemblyResolver = compilationExtension.GetAssemblyResolver(editor, file, null);
                    // but Weaver creates it's own if null, which is this one:
                    IAssemblyResolver assemblyResolver = new DefaultAssemblyResolver();
                    if (Program.Process(unityEngineCoreModuleDLL, mirrorRuntimeDll, outputDirectory, new string[] { assemblyPath }, GetExtraAssemblyPaths(assemblyPath), assemblyResolver, HandleWarning, HandleError))
                    {
                        WeaveFailed = false;
                        Console.WriteLine("Weaving succeeded for: " + assemblyPath);
                    }
                    else
                    {
                        WeaveFailed = true;
                        if (UnityLogEnabled) Debug.LogError("Weaving failed for: " + assemblyPath);
                    }
                }
            };
        }

        // Weaver needs the path for all the extra DLLs like UnityEngine.UI.
        // otherwise if a script that is being weaved (like a NetworkBehaviour)
        // uses UnityEngine.UI, then the Weaver won't be able to resolve it and
        // throw an error.
        // (the paths can be found by logging the extraAssemblyPaths in the
        //  original Weaver.Program.Process function.)
        static string[] GetExtraAssemblyPaths(string assemblyPath)
        {
            Assembly[] assemblies = CompilationPipeline.GetAssemblies();

            foreach (Assembly assembly in assemblies)
            {
                if (assembly.outputPath == assemblyPath)
                {
                    return assembly.compiledAssemblyReferences.Select(Path.GetDirectoryName).ToArray();
                }
            }

            if (UnityLogEnabled) Debug.LogWarning("Unable to find configuration for assembly " + assemblyPath);
            return new string[] { };
        }

        static string FindMirrorRuntime()
        {
            Assembly[] assemblies = CompilationPipeline.GetAssemblies();

            foreach (Assembly assembly in assemblies)
            {
                if (assembly.name == "Mirror")
                {
                    return assembly.outputPath;
                }
            }
            return "";
        }
    }
}
