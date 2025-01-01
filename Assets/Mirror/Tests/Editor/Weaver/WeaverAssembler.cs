using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor.Compilation;
using UnityEngine;

namespace Mirror.Weaver.Tests
{
    public class WeaverAssembler : MonoBehaviour
    {
        static string _outputDirectory;
        public static string OutputDirectory
        {
            get
            {
                if (string.IsNullOrEmpty(_outputDirectory))
                {
                    _outputDirectory = EditorHelper.FindPath<WeaverAssembler>();
                }
                return _outputDirectory;
            }
        }
        public static string OutputFile;
        public static HashSet<string> SourceFiles { get; private set; }
        public static bool AllowUnsafe;
        public static List<CompilerMessage> CompilerMessages { get; private set; }
        public static bool CompilerErrors { get; private set; }
        public static bool DeleteOutputOnClear;

        // static constructor to initialize static properties
        static WeaverAssembler()
        {
            SourceFiles = new HashSet<string>();
            CompilerMessages = new List<CompilerMessage>();
        }

        // Add a range of source files to compile
        public static void AddSourceFiles(string[] sourceFiles)
        {
            foreach (string src in sourceFiles)
            {
                SourceFiles.Add(Path.Combine(OutputDirectory, src));
            }
        }

        // Delete output dll / pdb / mdb
        public static void DeleteOutput()
        {
            // "x.dll" shortest possible dll name
            if (OutputFile.Length < 5)
            {
                return;
            }

            string projPathFile = Path.Combine(OutputDirectory, OutputFile);

            try
            {
                File.Delete(projPathFile);
            }
            catch {}

            try
            {
                File.Delete(Path.ChangeExtension(projPathFile, ".pdb"));
            }
            catch {}

            try
            {
                File.Delete(Path.ChangeExtension(projPathFile, ".dll.mdb"));
            }
            catch {}
        }

        // clear all settings except for referenced assemblies (which are cleared with ClearReferences)
        public static void Clear()
        {
            if (DeleteOutputOnClear)
            {
                DeleteOutput();
            }

            CompilerErrors = false;
            OutputFile = "";
            SourceFiles.Clear();
            CompilerMessages.Clear();
            AllowUnsafe = false;
            DeleteOutputOnClear = false;
        }

        public static void Build(Action<string> OnWarning, Action<string> OnError)
        {
#pragma warning disable 618
            AssemblyBuilder assemblyBuilder = new AssemblyBuilder(Path.Combine(OutputDirectory, OutputFile), SourceFiles.ToArray())
            {
                // "The type 'MonoBehaviour' is defined in an assembly that is not referenced"
                referencesOptions = ReferencesOptions.UseEngineModules
            };
#pragma warning restore 618
            if (AllowUnsafe)
            {
                assemblyBuilder.compilerOptions.AllowUnsafeCode = true;
            }

#if UNITY_2020_3_OR_NEWER
            // Unity automatically invokes ILPostProcessor after
            // AssemblyBuilder.Build() (on windows at least. not on mac).
            // => .buildFinished() below CompilerMessages would already contain
            //    the weaver messages, failing tests.
            // => SyncVarTests->SyncVarSyncList fails too if ILPP was
            //    already applied by Unity, and we apply it again.
            //
            // we need to not run ILPP for WeaverTests assemblies here.
            // -> we can't set member variables because Unity creates a new
            //    ILPP instance internally and invokes it
            // -> define is passed through ILPP though, and avoids static state.
            assemblyBuilder.additionalDefines = new []{ILPostProcessorHook.IgnoreDefine};
#endif

            assemblyBuilder.buildFinished += delegate (string assemblyPath, CompilerMessage[] compilerMessages)
            {
                // CompilerMessages from compiling the original test assembly.
                // note that we can see weaver messages here if Unity runs
                // ILPostProcessor after AssemblyBuilder.Build().
                // => that's why we pass the ignore define above.
                CompilerMessages.AddRange(compilerMessages);
                foreach (CompilerMessage cm in compilerMessages)
                {
                    if (cm.type == CompilerMessageType.Error)
                    {
                        Debug.LogError($"{cm.file}:{cm.line} -- {cm.message}");
                        CompilerErrors = true;
                    }
                }

#if UNITY_2020_3_OR_NEWER
                // on 2018/2019, CompilationFinishedHook weaves after building.
                // on 2020, ILPostProcessor weaves after building.
                //   on windows, it runs after AssemblyBuilder.Build()
                //   on mac, it does not run after AssemblyBuidler.Build()
                // => run it manually in all cases
                // => this way we can feed result.Logs to test results too
                // NOTE: we could simply call Weaver.Weave() here.
                //       but let's make all tests run through ILPP.
                //       just like regular projects would.
                //       helps catch issues early.

                // copy references from assemblyBuilder's references
                List<string> references = new List<string>();
                if (assemblyBuilder.defaultReferences != null)
                    references.AddRange(assemblyBuilder.defaultReferences);
                if (assemblyBuilder.additionalReferences != null)
                    references.AddRange(assemblyBuilder.additionalReferences);

                // invoke ILPostProcessor with an assembly from file.
                // NOTE: code for creating and invoking the ILPostProcessor has
                //       to be in Weaver.dll where 'CompilationPipeline' is
                //       available due to name being of form 'Unity.*.CodeGen'.
                //       => we can't change tests to that Unity.*.CodeGen
                //          because some tests need to be weaved, but ILPP isn't
                //          ran on Unity.*.CodeGen assemblies itself.
                ILPostProcessorFromFile.ILPostProcessFile(assemblyPath, references.ToArray(), OnWarning, OnError);
#endif
            };

            // Start build of assembly
            if (!assemblyBuilder.Build())
            {
                Debug.LogError($"Failed to start build of assembly {assemblyBuilder.assemblyPath}");
                return;
            }

            while (assemblyBuilder.status != AssemblyBuilderStatus.Finished)
            {
                Thread.Sleep(10);
            }
        }
    }
}
