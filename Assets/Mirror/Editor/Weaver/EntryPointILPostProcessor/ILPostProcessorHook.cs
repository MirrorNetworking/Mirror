// hook via ILPostProcessor from Unity 2020+
#if UNITY_2020_1_OR_NEWER
// Unity.CompilationPipeline reference is only resolved if assembly name is
// Unity.*.CodeGen:
// https://forum.unity.com/threads/how-does-unity-do-codegen-and-why-cant-i-do-it-myself.853867/#post-5646937
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.CecilX;
using Unity.CompilationPipeline.Common.Diagnostics;
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

        // can't Debug.Log in here. need to add to this list.
        public List<DiagnosticMessage> Logs = new List<DiagnosticMessage>();

        public void Log(string message, DiagnosticType logType = DiagnosticType.Warning)
        {
            Logs.Add(new DiagnosticMessage
            {
                // TODO add file etc. for double click opening later?
                DiagnosticType = logType, // doesn't have .Log
                File = null,
                Line = 0,
                Column = 0,
                MessageData = $"Weaver: {message}"
            });
        }

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
            //         Log($"{compiledAssembly.Name} references {reference}");
            return compiledAssembly.Name == MirrorRuntimeAssemblyName ||
                   compiledAssembly.References.Any(filePath => Path.GetFileNameWithoutExtension(filePath) == MirrorRuntimeAssemblyName);
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            Log($"Processing {compiledAssembly.Name}");
            TestWeave(compiledAssembly);
            return new ILPostProcessResult(compiledAssembly.InMemoryAssembly, Logs);
        }

        // test basic weaving
        void TestWeave(ICompiledAssembly compiledAssembly)
        {
            // load the InMemoryAssembly peData into a MemoryStream
            byte[] peData = compiledAssembly.InMemoryAssembly.PeData;
            Log($"  peData.Length={peData.Length} bytes");
            using (MemoryStream stream = new MemoryStream(peData))
            {
                if (Weaver.Weave(stream, "", compiledAssembly.References))
                {
                    Log($"Weaving succeeded for: {compiledAssembly.Name}");
                }
                else Log($"Weaving failed for: {compiledAssembly.Name}");
            }

            // TODO return the modified assembly
        }
    }
}
#endif
