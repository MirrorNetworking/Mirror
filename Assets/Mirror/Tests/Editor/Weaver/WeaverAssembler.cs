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
            AssemblyBuilder assemblyBuilder = new AssemblyBuilder(Path.Combine(OutputDirectory, OutputFile), SourceFiles.ToArray())
            {
                // "The type 'MonoBehaviour' is defined in an assembly that is not referenced"
                referencesOptions = ReferencesOptions.UseEngineModules
            };
            if (AllowUnsafe)
            {
                assemblyBuilder.compilerOptions.AllowUnsafeCode = true;
            }

            assemblyBuilder.buildFinished += delegate (string assemblyPath, CompilerMessage[] compilerMessages)
            {
#if !UNITY_2020_1_OR_NEWER
                // CompilerMessages from CompilationFinishedHook for Unity 2019.
                // on 2020, ILPostProcessor runs after AssemblyBuilder.Build on
                // windows, but not mac.
                // => on windows, we would see weaver errors in here.
                // => this would make tests fail
                // => simply ignore the first ILPP result.
                //    we run it manually below AND feed errors to tests.
                CompilerMessages.AddRange(compilerMessages);
                foreach (CompilerMessage cm in compilerMessages)
                {
                    if (cm.type == CompilerMessageType.Error)
                    {
                        Debug.LogError($"{cm.file}:{cm.line} -- {cm.message}");
                        CompilerErrors = true;
                    }
                }
#endif

#if UNITY_2020_1_OR_NEWER
                // on 2018/2019, CompilationFinishedHook weaved after building.
                // on 2020, ILPostProcessor weaves after building.
                //   on windows, it runs after AssemblyBuilder.Build()
                //   on mac, it does not run after AssemblyBuidler.Build()
                // => run it manually in all cases
                // => this way we can feed result.Logs to test results too

                // copy references from assemblyBuilder's references
                List<string> references = new List<string>();
                if (assemblyBuilder.defaultReferences != null)
                    references.AddRange(assemblyBuilder.defaultReferences);
                if (assemblyBuilder.additionalReferences != null)
                    references.AddRange(assemblyBuilder.additionalReferences);

                // invoke ILPostProcessor with an assembly from file.
                // we could Weave() with a test logger manually.
                // but for test result consistency on all platforms,
                // let's invoke the ILPostProcessor here too.
                // NOTE: CompilationPipeline can only be imported if this
                //       assembly's name is 'Unity.*.CodeGen'.
                // BUT:  then this assembly itself isn't weaved.
                //       we need it to be weaved for tests too though.
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
