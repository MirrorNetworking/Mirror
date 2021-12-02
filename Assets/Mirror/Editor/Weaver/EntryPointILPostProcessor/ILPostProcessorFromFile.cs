// helper function to use ILPostProcessor for an assembly from file.
// we keep this in Weaver folder because we can access CompilationPipleine here.
// in tests folder we can't, unless we rename to "Unity.*.CodeGen",
// but then tests wouldn't be weaved anymore.
#if UNITY_2020_3_OR_NEWER
using System;
using System.IO;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace Mirror.Weaver
{
    public static class ILPostProcessorFromFile
    {
        // read, weave, write file via ILPostProcessor
        public static void ILPostProcessFile(string assemblyPath, string[] references, Action<string> OnWarning, Action<string> OnError)
        {
            // we COULD Weave() with a test logger manually.
            // but for test result consistency on all platforms,
            // let's invoke the ILPostProcessor here too.
            CompiledAssemblyFromFile assembly = new CompiledAssemblyFromFile(assemblyPath);
            assembly.References = references;

            // create ILPP and check WillProcess like Unity would.
            ILPostProcessorHook ilpp = new ILPostProcessorHook();
            if (ilpp.WillProcess(assembly))
            {
                //Debug.Log($"Will Process: {assembly.Name}");

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

                // save the weaved assembly to file.
                // some tests open it and check for certain IL code.
                File.WriteAllBytes(assemblyPath, result.InMemoryAssembly.PeData);
            }
        }
    }
}
#endif
