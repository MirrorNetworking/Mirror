using Mono.Cecil;

namespace Mirror.Weaver
{
    internal class PostProcessorReflectionImporterProvider : IReflectionImporterProvider
    {
        public IReflectionImporter GetReflectionImporter(ModuleDefinition module)
        {
            return new PostProcessorReflectionImporter(module);
        }
    }
}