// hook via ILPostProcessor from Unity 2020+
#if UNITY_2020_1_OR_NEWER
// Unity.CompilationPipeline reference is only resolved if assembly name is
// Unity.*.CodeGen:
// https://forum.unity.com/threads/how-does-unity-do-codegen-and-why-cant-i-do-it-myself.853867/#post-5646937
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace Mirror.Weaver
{
    public class ILPostProcessorHook : ILPostProcessor
    {
        // ???
        public override ILPostProcessor GetInstance() => this;

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            return false;
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            throw new System.NotImplementedException();
        }
    }
}
#endif
