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
        public static bool Process(AssemblyDefinition CurrentAssembly, IAssemblyResolver resolver, Logger Log, Writers writers, Readers readers, ref bool WeavingFailed)
        {
            // find NetworkReader/Writer extensions from Mirror.dll first.
            // and NetworkMessage custom writer/reader extensions.
            // NOTE: do not include this result in our 'modified' return value,
            //       otherwise Unity crashes when running tests
            ProcessMirrorAssemblyClasses(CurrentAssembly, resolver, Log, writers, readers, ref WeavingFailed);

            // find readers/writers in the assembly we are in right now.
            return ProcessAssemblyClasses(CurrentAssembly, CurrentAssembly, writers, readers, ref WeavingFailed);
        }

        static void ProcessMirrorAssemblyClasses(AssemblyDefinition CurrentAssembly, IAssemblyResolver resolver, Logger Log, Writers writers, Readers readers, ref bool WeavingFailed)
        {
            // find Mirror.dll in assembly's references.
            // those are guaranteed to be resolvable and correct.
            // after all, it references them :)
            AssemblyNameReference mirrorAssemblyReference = CurrentAssembly.MainModule.FindReference(Weaver.MirrorAssemblyName);
            if (mirrorAssemblyReference != null)
            {
                // resolve the assembly to load the AssemblyDefinition.
                // we need to search all types in it.
                // if we only were to resolve one known type like in WeaverTypes,
                // then we wouldn't need it.
                AssemblyDefinition mirrorAssembly = resolver.Resolve(mirrorAssemblyReference);
                if (mirrorAssembly != null)
                {
                    ProcessAssemblyClasses(CurrentAssembly, mirrorAssembly, writers, readers, ref WeavingFailed);
                }
                else Log.Error($"Failed to resolve {mirrorAssemblyReference}");
            }
            else Log.Error("Failed to find Mirror AssemblyNameReference. Can't register Mirror.dll readers/writers.");
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

        // helper function to add [RuntimeInitializeOnLoad] attribute to method
        // TODO avoid reflection if possible
        // reflection is used because according paul, 'weaving Mirror.dll caused
        // unity to rebuild all dlls but in wrong order, which breaks rewired'
        static void AddRuntimeInitializeOnLoadAttribute(AssemblyDefinition assembly, WeaverTypes weaverTypes, MethodDefinition method)
        {
            System.Reflection.ConstructorInfo attributeconstructor = typeof(RuntimeInitializeOnLoadMethodAttribute).GetConstructor(new[] { typeof(RuntimeInitializeLoadType) });
            CustomAttribute customAttributeRef = new CustomAttribute(assembly.MainModule.ImportReference(attributeconstructor));
            customAttributeRef.ConstructorArguments.Add(new CustomAttributeArgument(weaverTypes.Import<RuntimeInitializeLoadType>(), RuntimeInitializeLoadType.BeforeSceneLoad));
            method.CustomAttributes.Add(customAttributeRef);
        }

        // helper function to add [InitializeOnLoad] attribute to method
        // (only works in Editor assemblies. check IsEditorAssembly first.)
        // TODO avoid reflection if possible
        // reflection is used because according paul, 'weaving Mirror.dll caused
        // unity to rebuild all dlls but in wrong order, which breaks rewired'
        static void AddInitializeOnLoadAttribute(AssemblyDefinition assembly, MethodDefinition method)
        {
            System.Reflection.ConstructorInfo initializeOnLoadConstructor = typeof(InitializeOnLoadMethodAttribute).GetConstructor(new Type[0]);
            CustomAttribute initializeCustomConstructorRef = new CustomAttribute(assembly.MainModule.ImportReference(initializeOnLoadConstructor));
            method.CustomAttributes.Add(initializeCustomConstructorRef);
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
            MethodDefinition initReadWriters = new MethodDefinition("InitReadWriters", MethodAttributes.Public |
                    MethodAttributes.Static,
                    weaverTypes.Import(typeof(void)));

            // add [RuntimeInitializeOnLoad] in any case
            AddRuntimeInitializeOnLoadAttribute(currentAssembly, weaverTypes, initReadWriters);

            // add [InitializeOnLoad] if UnityEditor is referenced
            if (IsEditorAssembly(currentAssembly))
            {
                AddInitializeOnLoadAttribute(currentAssembly, initReadWriters);
            }

            // fill function body with reader/writer initializers
            ILProcessor worker = initReadWriters.Body.GetILProcessor();
            // for debugging: add a log to see if initialized on load
            //worker.Emit(OpCodes.Ldstr, $"[InitReadWriters] called!");
            //worker.Emit(OpCodes.Call, Weaver.weaverTypes.logWarningReference);
            writers.InitializeWriters(worker);
            readers.InitializeReaders(worker);
            worker.Emit(OpCodes.Ret);

            GeneratedCodeClass.Methods.Add(initReadWriters);
        }
    }
}
