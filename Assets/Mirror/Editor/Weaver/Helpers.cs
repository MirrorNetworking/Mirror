using System.IO;
using System.Linq;
using System.Reflection;
using Mono.CecilX;
using Mono.CecilX.Rocks;
using UnityEngine;

namespace Mirror.Weaver
{
    static class Helpers
    {
        // This code is taken from SerializationWeaver
        public static string UnityEngineDllDirectoryName()
        {
            string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase);
            return directoryName?.Replace(@"file:\", "");
        }

        public static bool IsEditorAssembly(AssemblyDefinition currentAssembly)
        {
            // we want to add the [InitializeOnLoad] attribute if it's available
            // -> usually either 'UnityEditor' or 'UnityEditor.CoreModule'
            return currentAssembly.MainModule.AssemblyReferences.Any(assemblyReference =>
                assemblyReference.Name.StartsWith(nameof(UnityEditor))
            );
        }

        // helper function to add [RuntimeInitializeOnLoad] attribute to method
        public static void AddRuntimeInitializeOnLoadAttribute(AssemblyDefinition assembly, WeaverTypes weaverTypes, MethodDefinition method)
        {
            // NOTE: previously we used reflection because according paul,
            // 'weaving Mirror.dll caused unity to rebuild all dlls but in wrong
            //  order, which breaks rewired'
            // it's not obvious why importing an attribute via reflection instead
            // of cecil would break anything. let's use cecil.

            // to add a CustomAttribute, we need the attribute's constructor.
            // in this case, there are two: empty, and RuntimeInitializeOnLoadType.
            // we want the last one, with the type parameter.
            MethodDefinition ctor = weaverTypes.runtimeInitializeOnLoadMethodAttribute.GetConstructors().Last();
            //MethodDefinition ctor = weaverTypes.runtimeInitializeOnLoadMethodAttribute.GetConstructors().First();
            // using ctor directly throws: ArgumentException: Member 'System.Void UnityEditor.InitializeOnLoadMethodAttribute::.ctor()' is declared in another module and needs to be imported
            // we need to import it first.
            CustomAttribute attribute = new CustomAttribute(assembly.MainModule.ImportReference(ctor));
            // add the RuntimeInitializeLoadType.BeforeSceneLoad argument to ctor
            attribute.ConstructorArguments.Add(new CustomAttributeArgument(weaverTypes.Import<RuntimeInitializeLoadType>(), RuntimeInitializeLoadType.BeforeSceneLoad));
            method.CustomAttributes.Add(attribute);
        }

        // helper function to add [InitializeOnLoad] attribute to method
        // (only works in Editor assemblies. check IsEditorAssembly first.)
        public static void AddInitializeOnLoadAttribute(AssemblyDefinition assembly, WeaverTypes weaverTypes, MethodDefinition method)
        {
            // NOTE: previously we used reflection because according paul,
            // 'weaving Mirror.dll caused unity to rebuild all dlls but in wrong
            //  order, which breaks rewired'
            // it's not obvious why importing an attribute via reflection instead
            // of cecil would break anything. let's use cecil.

            // to add a CustomAttribute, we need the attribute's constructor.
            // in this case, there's only one - and it's an empty constructor.
            MethodDefinition ctor = weaverTypes.initializeOnLoadMethodAttribute.GetConstructors().First();
            // using ctor directly throws: ArgumentException: Member 'System.Void UnityEditor.InitializeOnLoadMethodAttribute::.ctor()' is declared in another module and needs to be imported
            // we need to import it first.
            CustomAttribute attribute = new CustomAttribute(assembly.MainModule.ImportReference(ctor));
            method.CustomAttributes.Add(attribute);
        }
    }
}
