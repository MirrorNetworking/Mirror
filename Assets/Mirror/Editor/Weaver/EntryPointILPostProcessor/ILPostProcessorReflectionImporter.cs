// based on paul's resolver from
// https://github.com/MirageNet/Mirage/commit/def64cd1db525398738f057b3d1eb1fe8afc540c?branch=def64cd1db525398738f057b3d1eb1fe8afc540c&diff=split
//
// ILPostProcessorAssemblyRESOLVER does not find the .dll file for:
// "System.Private.CoreLib"
// we need this custom reflection importer to fix that.
using System.Linq;
using System.Reflection;
using Mono.CecilX;

namespace Mirror.Weaver
{
    internal class ILPostProcessorReflectionImporter : DefaultReflectionImporter
    {
        const string SystemPrivateCoreLib = "System.Private.CoreLib";
        readonly AssemblyNameReference fixedCoreLib;

        public ILPostProcessorReflectionImporter(ModuleDefinition module) : base(module)
        {
            // find the correct library for "System.Private.CoreLib".
            // either mscorlib or netstandard.
            // defaults to System.Private.CoreLib if not found.
            fixedCoreLib = module.AssemblyReferences.FirstOrDefault(a => a.Name == "mscorlib" || a.Name == "netstandard" || a.Name == SystemPrivateCoreLib);
        }

        public override AssemblyNameReference ImportReference(AssemblyName name)
        {
            // System.Private.CoreLib?
            if (name.Name == SystemPrivateCoreLib && fixedCoreLib != null)
                return fixedCoreLib;

            // otherwise import as usual
            return base.ImportReference(name);
        }
    }
}
