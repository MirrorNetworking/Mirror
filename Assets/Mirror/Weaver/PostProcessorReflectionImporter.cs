using System.Linq;
using System.Reflection;
using Mono.Cecil;

namespace Mirror.Weaver
{

    internal class PostProcessorReflectionImporter : DefaultReflectionImporter
    {
        private const string SystemPrivateCoreLib = "System.Private.CoreLib";
        private readonly AssemblyNameReference _correctCorlib;

        public PostProcessorReflectionImporter(ModuleDefinition module) : base(module)
        {
            _correctCorlib = module.AssemblyReferences.FirstOrDefault(a => a.Name == "mscorlib" || a.Name == "netstandard" || a.Name == SystemPrivateCoreLib);
        }

        public override AssemblyNameReference ImportReference(AssemblyName name)
        {
            return _correctCorlib != null && name.Name == SystemPrivateCoreLib ? _correctCorlib : base.ImportReference(name);
        }
    }
}