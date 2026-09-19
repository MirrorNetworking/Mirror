using System.IO;
using System.Linq;
using Mono.CecilX;
#if UNITY_6000_0_OR_NEWER
using UnityEngine;
#endif

namespace Mirror.Weaver
{
    static class Helpers
    {
        // This code is taken from SerializationWeaver
        public static string UnityEngineDllDirectoryName()
        {
#if UNITY_6000_0_OR_NEWER
            // Unity loads many assemblies from streams, so Location/CodeBase are empty.
            // GetLoadedAssemblyPath() is Unity's mapping back to the original file.
            string assemblyPath = typeof(UnityEngine.Object).Assembly.GetLoadedAssemblyPath();
            if (!string.IsNullOrEmpty(assemblyPath))
                return Path.GetDirectoryName(assemblyPath);
#endif

            // Fallback used by CompilationFinishedHook for CoreModule
            string coreModule = UnityEditorInternal.InternalEditorUtility.GetEngineCoreModuleAssemblyPath();
            if (!string.IsNullOrEmpty(coreModule))
                return Path.GetDirectoryName(coreModule);

            return null;
        }

        public static bool IsEditorAssembly(AssemblyDefinition currentAssembly)
        {
            // we want to add the [InitializeOnLoad] attribute if it's available
            // -> usually either 'UnityEditor' or 'UnityEditor.CoreModule'
            return currentAssembly.MainModule.AssemblyReferences.Any(assemblyReference =>
                assemblyReference.Name.StartsWith(nameof(UnityEditor))
            );
        }
    }
}
