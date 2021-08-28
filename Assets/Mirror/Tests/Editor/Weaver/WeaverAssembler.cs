using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
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
                CompilerMessages.AddRange(compilerMessages);
                foreach (CompilerMessage cm in compilerMessages)
                {
                    if (cm.type == CompilerMessageType.Error)
                    {
                        Debug.LogError($"{cm.file}:{cm.line} -- {cm.message}");
                        CompilerErrors = true;
                    }
                }

#if UNITY_2020_1_OR_NEWER && UNITY_EDITOR_OSX
                // CompilationFinishedHook weaves test pre Unity 2020.
                // after Unity 2020, ILPostProcessor is invoked by AssemblyBuilder.
                // on mac, it is not invoked automatically.
                // need to do it manually until it's fixed by Unity.

                // TODO invoke ILPostProcessor manually
                // TODO save to file manually, so tests using the DLLs use the waved ones.

                // we COULD Weave() with a test logger manually.
                // but for test result consistency on all platforms,
                // let's invoke the ILPostProcessor here too.
                CompiledAssemblyFromFile assembly = new CompiledAssemblyFromFile(assemblyPath);
                // References needs to be set to something.
                // otherwise we get NullReferenceException in WillProcess while
                // checking References.
                // -> copy the AssemblyBuilder's references
                List<string> references = new List<string>();
                if (assemblyBuilder.defaultReferences != null)
                    references.AddRange(assemblyBuilder.defaultReferences);
                if (assemblyBuilder.additionalReferences != null)
                    references.AddRange(assemblyBuilder.additionalReferences);
                assembly.References = references.ToArray();

                // create ILPP and check WillProcess like Unity would.
                ILPostProcessorHook ilpp = new ILPostProcessorHook();
                if (ilpp.WillProcess(assembly))
                {
                    Debug.Log("Will Process: " + assembly.Name);

                    // process it like Unity would
                    ILPostProcessResult result = ilpp.Process(assembly);

                    // handle the error messages like Unity would
                    foreach (DiagnosticMessage message in result.Diagnostics)
                    {
                        if (message.DiagnosticType == DiagnosticType.Warning)
                        {
                            OnWarning(message.MessageData);
                        }
                        else if (message.DiagnosticType == DiagnosticType.Error)
                        {
                            OnError(message.MessageData);
                        }
                    }
                    // TODO need to feed them to weaverWarnings/weaverErrors

                    // TODO save to file. otherwise we still operate on unweaved assembly.
                }
                else
                {
                    Debug.LogWarning("WONT PROCESS: " + assembly.Name);
                }
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
