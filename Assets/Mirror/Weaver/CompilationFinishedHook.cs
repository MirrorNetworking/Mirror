// https://docs.unity3d.com/Manual/RunningEditorCodeOnLaunch.html
using System.IO;
using Mono.MirrorCecil;
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

        public static bool WeaverDisabled { get; set; } // controls whether we weave any assemblies when CompilationPipeline delegates are invoked
        public static bool UnityLogDisabled { get; set; } // controls weather Weaver errors are reported direct to the Unity console (tests enable this)
        public static bool WeaveFailed { get; private set; } // holds the result status of our latest Weave operation

        // the static constructor is called again if any of the cached information is changed
        static Assembly[] m_cachedAssemblies; // cached copy of CompilationPipeline.GetAssemblies
        static string m_cachedMirrorAssemblyPath; // cached Mirror.dll path
        static string m_cachedUnityEngineCoreAssemblyPath;
        static string[] m_excludedAssemblies = new string[]
        {
            "Telepathy.dll",
            "Mirror.dll",
            "Mirror.Weaver.dll",
            "Unity.Entities"
        };

        // constructor sets up cached assembly paths and adds our callback to trigger after an assembly compiles
        // CompilationPipeline.assemblyCompilationFinished will only be called on scripts that actually needed recompile
        // after all compilations are complete, all InitializeOnLoad methods (eg. this static constructor) will get called again
        static CompilationFinishedHook()
        {
            EditorApplication.LockReloadAssemblies();

            m_cachedAssemblies = CompilationPipeline.GetAssemblies();
            m_cachedMirrorAssemblyPath = FindMirrorRuntime();
            m_cachedUnityEngineCoreAssemblyPath = UnityEditorInternal.InternalEditorUtility.GetEngineCoreModuleAssemblyPath();

            // weave assemblies every time after they are compiled
            CompilationPipeline.assemblyCompilationFinished += AssemblyCompilationFinishedHandler;

            EditorApplication.UnlockReloadAssemblies();
        }

        // debug message handler that also calls OnMessageMethod delegate
        static void HandleMessage(string msg)
        {
            if (!UnityLogDisabled) Debug.Log(msg);
            OnWeaverMessage?.Invoke(msg);
        }

        // warning message handler that also calls OnWarningMethod delegate
        static void HandleWarning(string msg)
        {
            if (!UnityLogDisabled) Debug.LogWarning(msg);
            OnWeaverWarning?.Invoke(msg);
        }

        // error message handler that also calls OnErrorMethod delegate
        static void HandleError(string msg)
        {
            if (!UnityLogDisabled) Debug.LogError(msg);
            OnWeaverError?.Invoke(msg);
        }

        // weave all assemblies by manually triggering the callback
        static void WeaveAssemblies()
        {
            foreach (Assembly assembly in m_cachedAssemblies)
            {
                if (!UnityLogDisabled) Debug.Log("Weaving " + assembly.outputPath);
                AssemblyCompilationFinishedHandler(assembly.outputPath, new CompilerMessage[] { } );               
            }
        }

        // callback to perform weaving when called manually or by CompilationPipeline.assemblyCompilationFinished delegate being invoked
        static void AssemblyCompilationFinishedHandler(string assemblyPath, CompilerMessage[] messages)
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

            // don't weave anything in our array of excluded assemblies
            foreach (string excluded in m_excludedAssemblies)
            {
                if (assemblyName.Contains(excluded))
                {
                    return;
                }
            }

            // outputDirectory is the directory of assemblyPath
            string outputDirectory = Path.GetDirectoryName(assemblyPath);

            if (!File.Exists(m_cachedMirrorAssemblyPath))
            {
                // this is normal, it happens with any assembly that is built before mirror
                // such as unity packages or your own assemblies
                // those don't need to be weaved
                // if any assembly depends on mirror, then it will be built after
                return;
            }

            if (!UnityLogDisabled) Debug.Log("Weaving: " + assemblyName);
            Console.WriteLine("Weaving: " + assemblyPath);
            // assemblyResolver: unity uses this by default:
            //   ICompilationExtension compilationExtension = GetCompilationExtension();
            //   IAssemblyResolver assemblyResolver = compilationExtension.GetAssemblyResolver(editor, file, null);
            // but Weaver creates it's own if null, which is this one:
            IAssemblyResolver assemblyResolver = new DefaultAssemblyResolver();
            if (Program.Process(m_cachedUnityEngineCoreAssemblyPath, m_cachedMirrorAssemblyPath, outputDirectory, new string[] { assemblyPath }, GetExtraAssemblyPaths(assemblyPath), assemblyResolver, HandleWarning, HandleError))
            {
                WeaveFailed = false;
                Console.WriteLine("Weaving succeeded for: " + assemblyPath);
            }
            else
            {
                WeaveFailed = true;
                if (!UnityLogDisabled) Debug.LogError("Weaving failed for: " + assemblyPath);
            }
        }

        // Weaver needs the path for all the extra DLLs like UnityEngine.UI.
        // otherwise if a script that is being weaved (like a NetworkBehaviour)
        // uses UnityEngine.UI, then the Weaver won't be able to resolve it and
        // throw an error.
        // (the paths can be found by logging the extraAssemblyPaths in the
        //  original Weaver.Program.Process function.)
        static string[] GetExtraAssemblyPaths(string assemblyPath)
        {
            foreach (Assembly assembly in m_cachedAssemblies)
            {
                if (assembly.outputPath == assemblyPath)
                {
                    return assembly.compiledAssemblyReferences.Select(Path.GetDirectoryName).ToArray();
                }
            }

            if (!UnityLogDisabled) Debug.LogWarning("Unable to find configuration for assembly " + assemblyPath);
            return new string[] { };
        }

        // find Mirror assembly from cached CompilationPipeline.GetAssemblies array
        static string FindMirrorRuntime()
        {
            foreach (Assembly assembly in m_cachedAssemblies)
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
