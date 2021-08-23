// based on paul's resolver from
// https://github.com/MirageNet/Mirage/commit/def64cd1db525398738f057b3d1eb1fe8afc540c?branch=def64cd1db525398738f057b3d1eb1fe8afc540c&diff=split
//
// ILPostProcessorAssemblyRESOLVER does not find the .dll file for:
// "System.Private.CoreLib"
// we need this custom reflection importer to fix that.
using Mono.CecilX;

namespace Mirror.Weaver
{
    internal class ILPostProcessorReflectionImporterProvider : IReflectionImporterProvider
    {
        public IReflectionImporter GetReflectionImporter(ModuleDefinition module) =>
            new ILPostProcessorReflectionImporter(module);
    }
}
