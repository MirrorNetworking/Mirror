using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace Mirror.Weaver
{
    public abstract class ILegacyPostProcessor
    {
        public abstract bool WillProcess(ICompiledAssembly compiledAssembly);
        public abstract ILPostProcessResult Process(ICompiledAssembly compiledAssembly);
        public abstract ILegacyPostProcessor GetInstance();
    }
}