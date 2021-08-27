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

        public static void Build()
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
