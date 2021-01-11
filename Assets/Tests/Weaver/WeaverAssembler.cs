using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using Mono.Cecil;

namespace Mirror.Weaver.Tests
{
    public class CompiledAssembly : ICompiledAssembly
    {
        private readonly string assemblyPath;
        private InMemoryAssembly inMemoryAssembly;

        public CompiledAssembly(string assemblyPath)
        {
            this.assemblyPath = assemblyPath;
        }

        public InMemoryAssembly InMemoryAssembly
        {
            get
            {

                if (inMemoryAssembly == null)
                {
                    byte[] peData = File.ReadAllBytes(assemblyPath);

                    string pdbFileName = Path.GetFileNameWithoutExtension(assemblyPath) + ".pdb";

                    byte[] pdbData = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(assemblyPath), pdbFileName));

                    inMemoryAssembly = new InMemoryAssembly(peData, pdbData);
                }
                return inMemoryAssembly;
            }
        }

        public string Name => Path.GetFileNameWithoutExtension(assemblyPath);

        public string[] References { get; set; }

        public string[] Defines { get; set; }
    }


    public class WeaverAssembler : MonoBehaviour
    {
        static string _outputDirectory;
        public static string OutputDirectory
        {
            get
            {
                if (string.IsNullOrEmpty(_outputDirectory))
                {
                    string[] guidsFound = AssetDatabase.FindAssets($"t:Script " + nameof(WeaverAssembler));
                    if (guidsFound.Length == 1 && !string.IsNullOrEmpty(guidsFound[0]))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guidsFound[0]);
                        _outputDirectory = Path.GetDirectoryName(path);
                    }
                    else
                    {
                        Debug.LogError("Could not find WeaverAssembler for Weaver Tests");
                    }
                }
                return _outputDirectory;
            }
        }
        public static string OutputFile { get; set; }
        public static HashSet<string> SourceFiles { get; private set; }
        public static HashSet<string> ReferenceAssemblies { get; private set; }
        public static bool AllowUnsafe { get; set; }
        public static List<CompilerMessage> CompilerMessages { get; private set; }
        public static bool CompilerErrors { get; private set; }
        public static bool DeleteOutputOnClear { get; set; }

        // static constructor to initialize static properties
        static WeaverAssembler()
        {
            SourceFiles = new HashSet<string>();
            ReferenceAssemblies = new HashSet<string>();
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

        // Find reference assembly specified by asmName and store its full path in asmFullPath
        // do not pass in paths in asmName, just assembly names
        public static bool FindReferenceAssemblyPath(string asmName, out string asmFullPath)
        {
            asmFullPath = "";

            Assembly[] asms = CompilationPipeline.GetAssemblies();
            foreach (Assembly asm in asms)
            {
                foreach (string asmRef in asm.compiledAssemblyReferences)
                {
                    if (asmRef.EndsWith(asmName))
                    {
                        asmFullPath = asmRef;
                        return true;
                    }
                }
            }

            return false;
        }

        // Add reference (not cleared during calls to Clear)
        public static void ClearReferences()
        {
            ReferenceAssemblies.Clear();
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
            catch { /* Do Nothing */ }

            try
            {
                File.Delete(Path.ChangeExtension(projPathFile, ".pdb"));
            }
            catch { /* Do Nothing */ }

            try
            {
                File.Delete(Path.ChangeExtension(projPathFile, ".dll.mdb"));
            }
            catch { /* Do Nothing */ }
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

       

        public static AssemblyDefinition Build(IWeaverLogger logger)
        {
            AssemblyDefinition assembly = null;

            var assemblyBuilder = new AssemblyBuilder(Path.Combine(OutputDirectory, OutputFile), SourceFiles.ToArray())
            {
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
                        Debug.LogErrorFormat("{0}:{1} -- {2}", cm.file, cm.line, cm.message);
                        CompilerErrors = true;
                    }
                }

                // assembly builder does not call ILPostProcessor (WTF Unity?),  so we must invoke it ourselves.
                var compiledAssembly = new CompiledAssembly(assemblyPath)
                {
                    Defines = assemblyBuilder.defaultDefines,
                    References = assemblyBuilder.defaultReferences
                };

                var weaver = new Weaver(logger);

                assembly = weaver.Weave(compiledAssembly);
            };

            // Start build of assembly
            if (!assemblyBuilder.Build())
            {
                Debug.LogErrorFormat("Failed to start build of assembly {0}", assemblyBuilder.assemblyPath);
                return assembly;
            }

            while (assemblyBuilder.status != AssemblyBuilderStatus.Finished)
            {
                System.Threading.Thread.Sleep(10);
            }

            return assembly;
        }
    }
}
