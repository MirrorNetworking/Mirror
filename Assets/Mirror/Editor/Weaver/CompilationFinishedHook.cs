using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityAssembly = UnityEditor.Compilation.Assembly;

namespace Mirror.Weaver
{
    public static class CompilationFinishedHook
    {
        const string MirrorRuntimeAssemblyName = "Mirror";
        const string MirrorWeaverAssemblyName = "Mirror.Weaver";

        // controls whether we weave any assemblies when CompilationPipeline delegates are invoked
        public static bool WeaverEnabled { get; set; }

        public static IWeaverLogger logger;

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

            EditorUtility.RequestScriptReload();
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
            if (assemblyName == MirrorRuntimeAssemblyName || assemblyName == MirrorWeaverAssemblyName || assemblyName == "Zenject")
            {
                return;
            }

            UnityAssembly assembly = CompilationPipeline.GetAssemblies().FirstOrDefault(ass => ass.outputPath == assemblyPath);

            if (assembly == null)
            {
                // no assembly found, this can happen if you use the AssemblyBuilder
                // happens with our weaver tests.
                // create an assembly object manually

                assembly = CreateUnityAssembly(assemblyPath);
            }

            // don't weave if this does not depend on mirror
            if (!assembly.allReferences.Any(path => Path.GetFileNameWithoutExtension(path) == MirrorRuntimeAssemblyName))
                return;


            var weaver = new Weaver(logger ?? new Logger());

            if (!weaver.WeaveAssembly(assembly))
            {
                // Set false...will be checked in \Editor\EnterPlayModeSettingsCheck.CheckSuccessfulWeave()
                SessionState.SetBool("MIRROR_WEAVE_SUCCESS", false);
            }
        }

        private static UnityAssembly CreateUnityAssembly(string assemblyPath)
        {
            // copy from one of the assemblies
            UnityAssembly mirrordll = CompilationPipeline.GetAssemblies().First(assembly => assembly.name=="Mirror");

            return new UnityAssembly(
                Path.GetFileNameWithoutExtension(assemblyPath),
                assemblyPath,
                new string[] { },
                CompilationPipeline.GetDefinesFromAssemblyName(assemblyPath),
                CompilationPipeline.GetAssemblies(),
                mirrordll.compiledAssemblyReferences,
                AssemblyFlags.None) ;
        }
    }
}
