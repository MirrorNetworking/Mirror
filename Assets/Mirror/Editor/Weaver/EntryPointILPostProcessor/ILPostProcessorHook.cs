// hook via ILPostProcessor from Unity 2020+
#if UNITY_2020_1_OR_NEWER
// Unity.CompilationPipeline reference is only resolved if assembly name is
// Unity.*.CodeGen:
// https://forum.unity.com/threads/how-does-unity-do-codegen-and-why-cant-i-do-it-myself.853867/#post-5646937
using System.Collections.Generic;
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

        public void Log(string message)
        {
            Logs.Add(new DiagnosticMessage
            {
                // TODO add file etc. for double click opening later?
                DiagnosticType = DiagnosticType.Warning, // doesn't have .Log
                File = null,
                Line = 0,
                Column = 0,
                MessageData = message
            });
        }

        // ???
        public override ILPostProcessor GetInstance() => this;

        // TODO process Mirror, or anything that references Mirror
        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            Log($"Considering {compiledAssembly.Name}");
            return true;
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            Log($"Processing {compiledAssembly.Name}");
            byte[] peData = new byte[0];
            byte[] pdbData = new byte[0];
            return new ILPostProcessResult(new InMemoryAssembly(peData, pdbData), Logs);
        }
    }
}
#endif
