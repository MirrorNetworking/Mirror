using System;
using Mono.Cecil;

namespace Mirror.Weaver
{
    public static class WeaverTypes
    {
        // custom attribute types

        private static AssemblyDefinition currentAssembly;

        public static void SetupTargetTypes(AssemblyDefinition currentAssembly)
        {
            // system types
            WeaverTypes.currentAssembly = currentAssembly;
        }
    }
}
