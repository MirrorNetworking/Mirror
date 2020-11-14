using System;
using Mono.Cecil;

namespace Mirror.Weaver
{
    public static class WeaverTypes
    {
        // custom attribute types

        private static AssemblyDefinition currentAssembly;

        public static TypeReference Import(Type t) => currentAssembly.MainModule.ImportReference(t);

        public static void SetupTargetTypes(AssemblyDefinition currentAssembly)
        {
            // system types
            WeaverTypes.currentAssembly = currentAssembly;
        }
    }
}
