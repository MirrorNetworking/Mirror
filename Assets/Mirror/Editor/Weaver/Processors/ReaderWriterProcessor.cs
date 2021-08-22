// finds all readers and writers and register them
using System;
using System.Linq;
using Mono.CecilX;
using Mono.CecilX.Cil;
using UnityEditor;
using UnityEngine;

namespace Mirror.Weaver
{
    public static class ReaderWriterProcessor
    {
        public static bool Process(AssemblyDefinition CurrentAssembly, AssemblyDefinition mirrorAssembly, Writers writers, Readers readers, ref bool WeavingFailed)
        {
            // find NetworkReader/Writer extensions from Mirror.dll first.
            // and NetworkMessage custom writer/reader extensions.
            // NOTE: do not include this result in our 'modified' return value,
            //       otherwise Unity crashes when running tests
            ProcessAssemblyClasses(CurrentAssembly, mirrorAssembly, writers, readers, ref WeavingFailed);

            // find readers/writers in the assembly we are in right now.
            return ProcessAssemblyClasses(CurrentAssembly, CurrentAssembly, writers, readers, ref WeavingFailed);
        }

        static bool ProcessAssemblyClasses(AssemblyDefinition CurrentAssembly, AssemblyDefinition assembly, Writers writers, Readers readers, ref bool WeavingFailed)
        {
            bool modified = false;
            foreach (TypeDefinition klass in assembly.MainModule.Types)
            {
                // extension methods only live in static classes
                // static classes are represented as sealed and abstract
                if (klass.IsAbstract && klass.IsSealed)
                {
                    // if assembly has any declared writers then it is "modified"
                    modified |= LoadDeclaredWriters(CurrentAssembly, klass, writers);
                    modified |= LoadDeclaredReaders(CurrentAssembly, klass, readers);
                }
            }

            foreach (TypeDefinition klass in assembly.MainModule.Types)
            {
                // if assembly has any network message then it is modified
                modified |= LoadMessageReadWriter(CurrentAssembly.MainModule, writers, readers, klass, ref WeavingFailed);
            }
            return modified;
        }

        static bool LoadMessageReadWriter(ModuleDefinition module, Writers writers, Readers readers, TypeDefinition klass, ref bool WeavingFailed)
        {
            bool modified = false;
            if (!klass.IsAbstract && !klass.IsInterface && klass.ImplementsInterface<NetworkMessage>())
            {
                readers.GetReadFunc(module.ImportReference(klass), ref WeavingFailed);
                writers.GetWriteFunc(module.ImportReference(klass), ref WeavingFailed);
                modified = true;
            }

            foreach (TypeDefinition td in klass.NestedTypes)
            {
                modified |= LoadMessageReadWriter(module, writers, readers, td, ref WeavingFailed);
            }
            return modified;
        }

        static bool LoadDeclaredWriters(AssemblyDefinition currentAssembly, TypeDefinition klass, Writers writers)
        {
            // register all the writers in this class.  Skip the ones with wrong signature
            bool modified = false;
            foreach (MethodDefinition method in klass.Methods)
            {
                if (method.Parameters.Count != 2)
                    continue;

                if (!method.Parameters[0].ParameterType.Is<NetworkWriter>())
                    continue;

                if (!method.ReturnType.Is(typeof(void)))
                    continue;

                if (!method.HasCustomAttribute<System.Runtime.CompilerServices.ExtensionAttribute>())
                    continue;

                if (method.HasGenericParameters)
                    continue;

                TypeReference dataType = method.Parameters[1].ParameterType;
                writers.Register(dataType, currentAssembly.MainModule.ImportReference(method));
                modified = true;
            }
            return modified;
        }

        static bool LoadDeclaredReaders(AssemblyDefinition currentAssembly, TypeDefinition klass, Readers readers)
        {
            // register all the reader in this class.  Skip the ones with wrong signature
            bool modified = false;
            foreach (MethodDefinition method in klass.Methods)
            {
                if (method.Parameters.Count != 1)
                    continue;

                if (!method.Parameters[0].ParameterType.Is<NetworkReader>())
                    continue;

                if (method.ReturnType.Is(typeof(void)))
                    continue;

                if (!method.HasCustomAttribute<System.Runtime.CompilerServices.ExtensionAttribute>())
                    continue;

                if (method.HasGenericParameters)
                    continue;

                readers.Register(method.ReturnType, currentAssembly.MainModule.ImportReference(method));
                modified = true;
            }
            return modified;
        }

        static bool IsEditorAssembly(AssemblyDefinition currentAssembly)
        {
            // we want to add the [InitializeOnLoad] attribute if it's available
            // -> usually either 'UnityEditor' or 'UnityEditor.CoreModule'
            return currentAssembly.MainModule.AssemblyReferences.Any(assemblyReference =>
                assemblyReference.Name.StartsWith(nameof(UnityEditor))
                );
        }

        // adds Mirror.GeneratedNetworkCode.InitReadWriters() method that
        // registers all generated writers into Mirror.Writer<T> static class.
        // -> uses [RuntimeInitializeOnLoad] attribute so it's invoke at runtime
        // -> uses [InitializeOnLoad] if UnityEditor is referenced so it works
        //    in Editor and in tests too
        //
        // use ILSpy to see the result (it's in the DLL's 'Mirror' namespace)
        public static void InitializeReaderAndWriters(AssemblyDefinition currentAssembly, WeaverTypes weaverTypes, Writers writers, Readers readers, TypeDefinition GeneratedCodeClass)
        {
            MethodDefinition rwInitializer = new MethodDefinition("InitReadWriters", MethodAttributes.Public |
                    MethodAttributes.Static,
                    weaverTypes.Import(typeof(void)));

            // add [RuntimeInitializeOnLoad] in any case
            System.Reflection.ConstructorInfo attributeconstructor = typeof(RuntimeInitializeOnLoadMethodAttribute).GetConstructor(new[] { typeof(RuntimeInitializeLoadType) });
            CustomAttribute customAttributeRef = new CustomAttribute(currentAssembly.MainModule.ImportReference(attributeconstructor));
            customAttributeRef.ConstructorArguments.Add(new CustomAttributeArgument(weaverTypes.Import<RuntimeInitializeLoadType>(), RuntimeInitializeLoadType.BeforeSceneLoad));
            rwInitializer.CustomAttributes.Add(customAttributeRef);

            // add [InitializeOnLoad] if UnityEditor is referenced
            if (IsEditorAssembly(currentAssembly))
            {
                System.Reflection.ConstructorInfo initializeOnLoadConstructor = typeof(InitializeOnLoadMethodAttribute).GetConstructor(new Type[0]);
                CustomAttribute initializeCustomConstructorRef = new CustomAttribute(currentAssembly.MainModule.ImportReference(initializeOnLoadConstructor));
                rwInitializer.CustomAttributes.Add(initializeCustomConstructorRef);
            }

            // fill function body with reader/writer initializers
            ILProcessor worker = rwInitializer.Body.GetILProcessor();

            // for debugging: add a log to see if initialized on load
            //worker.Emit(OpCodes.Ldstr, $"[InitReadWriters] called!");
            //worker.Emit(OpCodes.Call, Weaver.weaverTypes.logWarningReference);

            writers.InitializeWriters(worker);
            readers.InitializeReaders(worker);

            worker.Emit(OpCodes.Ret);

            GeneratedCodeClass.Methods.Add(rwInitializer);
        }
    }
}
