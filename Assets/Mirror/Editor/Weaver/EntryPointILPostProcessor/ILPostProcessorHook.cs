// hook via ILPostProcessor from Unity 2020+
#if UNITY_2020_1_OR_NEWER
// Unity.CompilationPipeline reference is only resolved if assembly name is
// Unity.*.CodeGen:
// https://forum.unity.com/threads/how-does-unity-do-codegen-and-why-cant-i-do-it-myself.853867/#post-5646937
using System.IO;
using System.Linq;
// to use Mono.CecilX here, we need to 'override references' in the
// Unity.Mirror.CodeGen assembly definition file in the Editor, and add CecilX.
// otherwise we get a reflection exception with 'file not found: CecilX'.
using Mono.CecilX;
using Unity.CompilationPipeline.Common.ILPostProcessing;
// IMPORTANT: 'using UnityEngine' does not work in here.
// Unity gives "(0,0): error System.Security.SecurityException: ECall methods must be packaged into a system module."
//using UnityEngine;

namespace Mirror.Weaver
{
    public class ILPostProcessorHook : ILPostProcessor
    {
        // from CompilationFinishedHook
        const string MirrorRuntimeAssemblyName = "Mirror";

        // we can't use Debug.Log in ILPP, so we need a custom logger
        ILPostProcessorLogger Log = new ILPostProcessorLogger();

        // ???
        public override ILPostProcessor GetInstance() => this;

        // process Mirror, or anything that references Mirror
        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            // compiledAssembly.References are file paths:
            //   Library/Bee/artifacts/200b0aE.dag/Mirror.CompilerSymbols.dll
            //   Assets/Mirror/Plugins/Mono.Cecil/Mono.CecilX.dll
            //   /Applications/Unity/Hub/Editor/2021.2.0b6_apple_silicon/Unity.app/Contents/NetStandard/ref/2.1.0/netstandard.dll
            //
            // log them to see:
            //     foreach (string reference in compiledAssembly.References)
            //         LogDiagnostics($"{compiledAssembly.Name} references {reference}");
            return compiledAssembly.Name == MirrorRuntimeAssemblyName ||
                   compiledAssembly.References.Any(filePath => Path.GetFileNameWithoutExtension(filePath) == MirrorRuntimeAssemblyName);
        }

        // searches for Mirror.dll in references.
        // note that for Mirror.dll itself, it won't find anything.
        string FindMirrorRuntimeReference(ICompiledAssembly compiledAssembly)
        {
            return compiledAssembly.References.ToList().Find(filePath => Path.GetFileNameWithoutExtension(filePath) == MirrorRuntimeAssemblyName);
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            Log.Warning($"Processing {compiledAssembly.Name}");

            // load the InMemoryAssembly peData into a MemoryStream
            byte[] peData = compiledAssembly.InMemoryAssembly.PeData;
            //LogDiagnostics($"  peData.Length={peData.Length} bytes");
            using (MemoryStream stream = new MemoryStream(peData))
            using (ILPostProcessorAssemblyResolver asmResolver = new ILPostProcessorAssemblyResolver(compiledAssembly, Log))
            {
                // we need to load symbols. otherwise we get:
                // "(0,0): error Mono.CecilX.Cil.SymbolsNotFoundException: No symbol found for file: "
                using (MemoryStream symbols = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData))
                {
                    ReaderParameters readerParameters = new ReaderParameters{
                        SymbolStream = symbols,
                        ReadWrite = true,
                        ReadSymbols = true,
                        AssemblyResolver = asmResolver,
                        // custom reflection importer to fix System.Private.CoreLib
                        // not being found in custom assembly resolver above.
                        ReflectionImporterProvider = new ILPostProcessorReflectionImporterProvider()
                    };
                    using (AssemblyDefinition asmDef = AssemblyDefinition.ReadAssembly(stream, readerParameters))
                    {
                        // TODO see if we need this.
                        // supposedly fixes resolving a type from Mirror.dll while processing Mirror.dll?
                        // asmResolver.AddAssemblyDefinitionBeingOperatedOn(asmDef);

                        Weaver weaver = new Weaver(Log);

                        // TODO add dependencies?

                        // we need Mirror.dll AssemblyDefinition too
                        // find it in references...
                        // 'Library/Bee/artifacts/200b0aE.dag/Mirror.dll' usually
                        string mirrorAssemblyPath = FindMirrorRuntimeReference(compiledAssembly);

                        // did we find Mirror.dll in references?
                        if (mirrorAssemblyPath != null)
                        {
                            Log.Warning("Mirror Ref: " + mirrorAssemblyPath);
                            // resolve mirror assembly
                            // TODO is this safe with ILPP multithreading though?
                            // (it sure has to exist while we process this assembly since it's referenced)
                            // TODO use our custom resolver. default fails to resolve in ReaderWriterProcessor.LoadMessageReadWriter -> IsInterface extension
                            using (DefaultAssemblyResolver mirrorAsmResolver = new DefaultAssemblyResolver())
                            using (AssemblyDefinition mirrorAssembly = AssemblyDefinition.ReadAssembly(mirrorAssemblyPath, new ReaderParameters { ReadWrite = false, ReadSymbols = false, AssemblyResolver = mirrorAsmResolver }))
                            {
                                // weave this assembly. and pass mirror.dll.
                                if (weaver.Weave(asmDef, mirrorAssembly))
                                {
                                    Log.Warning($"Weaving succeeded for: {compiledAssembly.Name}");
                                    // TODO return modified assembly
                                    // TODO AND pdb / debug symbols
                                }
                                else Log.Error($"Weaving failed for: {compiledAssembly.Name}");
                            }
                        }
                        // we ARE Mirror.dll
                        else
                        {
                            Log.Warning("Mirror Itself: " + compiledAssembly.Name);
                            // weave this assembly. and mirror.dll is this assembly.
                            if (weaver.Weave(asmDef, asmDef))
                            {
                                Log.Warning($"Weaving succeeded for: {compiledAssembly.Name}");
                                // TODO return modified assembly
                                // TODO AND pdb / debug symbols
                            }
                            else Log.Error($"Weaving failed for: {compiledAssembly.Name}");
                        }
                    }
                }
            }

            // TODO needs modified assembly
            return new ILPostProcessResult(compiledAssembly.InMemoryAssembly, Log.Logs);
        }
    }
}
#endif
