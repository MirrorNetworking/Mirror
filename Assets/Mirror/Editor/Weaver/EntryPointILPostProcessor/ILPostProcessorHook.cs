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
using Mono.CecilX.Cil;
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

        // ILPostProcessor is invoked by Unity.
        // we can not tell it to ignore certain assemblies before processing.
        // add a 'ignore' define for convenience.
        // => WeaverTests/WeaverAssembler need it to avoid Unity running it
        public const string IgnoreDefine = "ILPP_IGNORE";

        // we can't use Debug.Log in ILPP, so we need a custom logger
        ILPostProcessorLogger Log = new ILPostProcessorLogger();

        // ???
        public override ILPostProcessor GetInstance() => this;

        // check if assembly has the 'ignore' define
        static bool HasDefine(ICompiledAssembly assembly, string define) =>
            assembly.Defines != null &&
            assembly.Defines.Contains(define);

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
            bool relevant = compiledAssembly.Name == MirrorRuntimeAssemblyName ||
                            compiledAssembly.References.Any(filePath => Path.GetFileNameWithoutExtension(filePath) == MirrorRuntimeAssemblyName);
            bool ignore = HasDefine(compiledAssembly, IgnoreDefine);
            return relevant && !ignore;
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            //Log.Warning($"Processing {compiledAssembly.Name}");

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
                        // resolving a Mirror.dll type like NetworkServer while
                        // weaving Mirror.dll does not work. it throws a
                        // NullReferenceException in WeaverTypes.ctor
                        // when Resolve() is called on the first Mirror type.
                        // need to add the AssemblyDefinition itself to use.
                        asmResolver.SetAssemblyDefinitionForCompiledAssembly(asmDef);

                        // weave this assembly.
                        Weaver weaver = new Weaver(Log);
                        if (weaver.Weave(asmDef, asmResolver, out bool modified))
                        {
                            //Log.Warning($"Weaving succeeded for: {compiledAssembly.Name}");

                            // write if modified
                            if (modified)
                            {
                                // when weaving Mirror.dll with ILPostProcessor,
                                // Weave() -> WeaverTypes -> resolving the first
                                // type in Mirror.dll adds a reference to
                                // Mirror.dll even though we are in Mirror.dll.
                                // -> this would throw an exception:
                                //    "Mirror references itself" and not compile
                                // -> need to detect and fix manually here
                                if (asmDef.MainModule.AssemblyReferences.Any(r => r.Name == asmDef.Name.Name))
                                {
                                    asmDef.MainModule.AssemblyReferences.Remove(asmDef.MainModule.AssemblyReferences.First(r => r.Name == asmDef.Name.Name));
                                    //Log.Warning($"fixed self referencing Assembly: {asmDef.Name.Name}");
                                }

                                MemoryStream peOut = new MemoryStream();
                                MemoryStream pdbOut = new MemoryStream();
                                WriterParameters writerParameters = new WriterParameters
                                {
                                    SymbolWriterProvider = new PortablePdbWriterProvider(),
                                    SymbolStream = pdbOut,
                                    WriteSymbols = true
                                };

                                asmDef.Write(peOut, writerParameters);

                                InMemoryAssembly inMemory = new InMemoryAssembly(peOut.ToArray(), pdbOut.ToArray());
                                return new ILPostProcessResult(inMemory, Log.Logs);
                            }
                        }
                        // if anything during Weave() fails, we log an error.
                        // don't need to indicate 'weaving failed' again.
                        // in fact, this would break tests only expecting certain errors.
                        //else Log.Error($"Weaving failed for: {compiledAssembly.Name}");
                    }
                }
            }

            // always return an ILPostProcessResult with Logs.
            // otherwise we won't see Logs if weaving failed.
            return new ILPostProcessResult(compiledAssembly.InMemoryAssembly, Log.Logs);
        }
    }
}
#endif
