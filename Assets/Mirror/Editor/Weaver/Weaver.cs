using System;
using System.Collections.Generic;
using System.Diagnostics;
using Mono.CecilX;
using Mono.CecilX.Cil;
using Mono.CecilX.Rocks;

namespace Mirror.Weaver
{
    // not static, because ILPostProcessor is multithreaded
    internal class Weaver
    {
        // generated code class
        public const string GeneratedCodeNamespace = "Mirror";
        public const string GeneratedCodeClassName = "GeneratedNetworkCode";
        TypeDefinition GeneratedCodeClass;

        // for resolving Mirror.dll in ReaderWriterProcessor, we need to know
        // Mirror.dll name
        public const string MirrorAssemblyName = "Mirror";

        WeaverTypes weaverTypes;
        SyncVarAccessLists syncVarAccessLists;
        AssemblyDefinition CurrentAssembly;
        Writers writers;
        Readers readers;

        // in case of weaver errors, we don't stop immediately.
        // we log all errors and then eventually return false if
        // weaving has failed.
        // this way the user can fix multiple errors at once, instead of having
        // to fix -> recompile -> fix -> recompile for one error at a time.
        bool WeavingFailed;

        // logger functions can be set from the outside.
        // for example, Debug.Log or ILPostProcessor Diagnostics log for
        // multi threaded logging.
        public Logger Log;

        // remote actions now support overloads,
        // -> but IL2CPP doesnt like it when two generated methods
        // -> have the same signature,
        // -> so, append the signature to the generated method name,
        // -> to create a unique name
        // Example:
        // RpcTeleport(Vector3 position) -> InvokeUserCode_RpcTeleport__Vector3()
        // RpcTeleport(Vector3 position, Quaternion rotation) -> InvokeUserCode_RpcTeleport__Vector3Quaternion()
        // fixes https://github.com/vis2k/Mirror/issues/3060
        public static string GenerateMethodName(string initialPrefix, MethodDefinition md)
        {
            initialPrefix += md.Name;

            for (int i = 0; i < md.Parameters.Count; ++i)
            {
                // with __ so it's more obvious that this is the parameter suffix.
                // otherwise RpcTest(int) => RpcTestInt(int) which is not obvious.
                initialPrefix += $"__{md.Parameters[i].ParameterType.Name}";
            }

            return initialPrefix;
        }

        public Weaver(Logger Log)
        {
            this.Log = Log;
        }

        // returns 'true' if modified (=if we did anything)
        bool WeaveNetworkBehavior(TypeDefinition td)
        {
            if (!td.IsClass)
                return false;

            if (!td.IsDerivedFrom<NetworkBehaviour>())
            {
                if (td.IsDerivedFrom<UnityEngine.MonoBehaviour>())
                    MonoBehaviourProcessor.Process(Log, td, ref WeavingFailed);
                return false;
            }

            // process this and base classes from parent to child order

            List<TypeDefinition> behaviourClasses = new List<TypeDefinition>();

            TypeDefinition parent = td;
            while (parent != null)
            {
                if (parent.Is<NetworkBehaviour>())
                {
                    break;
                }

                try
                {
                    behaviourClasses.Insert(0, parent);
                    parent = parent.BaseType.Resolve();
                }
                catch (AssemblyResolutionException)
                {
                    // this can happen for plugins.
                    //Console.WriteLine("AssemblyResolutionException: "+ ex.ToString());
                    break;
                }
            }

            bool modified = false;
            foreach (TypeDefinition behaviour in behaviourClasses)
            {
                modified |= new NetworkBehaviourProcessor(CurrentAssembly, weaverTypes, syncVarAccessLists, writers, readers, Log, behaviour).Process(ref WeavingFailed);
            }
            return modified;
        }

        bool WeaveModule(ModuleDefinition moduleDefinition)
        {
            bool modified = false;

            Stopwatch watch = Stopwatch.StartNew();
            watch.Start();

            // ModuleDefinition.Types only finds top level types.
            // GetAllTypes recursively finds all nested types as well.
            // fixes nested types not being weaved, for example:
            //     class Parent {              // ModuleDefinition.Types finds this
            //         class Child {           // .Types.NestedTypes finds this
            //             class GrandChild {} // only GetAllTypes finds this too
            //         }
            //     }
            // note this is not about inheritance, only about type definitions.
            // see test: NetworkBehaviourTests.DeeplyNested()
            foreach (TypeDefinition td in moduleDefinition.GetAllTypes())
            {
                if (td.IsClass && td.BaseType.CanBeResolved())
                {
                    modified |= WeaveNetworkBehavior(td);
                    modified |= ServerClientAttributeProcessor.Process(weaverTypes, Log, td, ref WeavingFailed);
                }
            }

            watch.Stop();
            Console.WriteLine($"Weave behaviours and messages took {watch.ElapsedMilliseconds} milliseconds");

            return modified;
        }

        void CreateGeneratedCodeClass()
        {
            // create "Mirror.GeneratedNetworkCode" class which holds all
            // Readers<T> and Writers<T>
            GeneratedCodeClass = new TypeDefinition(GeneratedCodeNamespace, GeneratedCodeClassName,
                TypeAttributes.BeforeFieldInit | TypeAttributes.Class | TypeAttributes.AnsiClass | TypeAttributes.Public | TypeAttributes.AutoClass | TypeAttributes.Abstract | TypeAttributes.Sealed,
                weaverTypes.Import<object>());
        }

        void ToggleWeaverFuse()
        {
            // // find Weaved() function
            MethodDefinition func = weaverTypes.weaverFuseMethod.Resolve();
            // // change return 0 to return 1

            ILProcessor worker = func.Body.GetILProcessor();
            func.Body.Instructions[0] = worker.Create(OpCodes.Ldc_I4_1);
        }

        // Weave takes an AssemblyDefinition to be compatible with both old and
        // new weavers:
        // * old takes a filepath, new takes a in-memory byte[]
        // * old uses DefaultAssemblyResolver with added dependencies paths,
        //   new uses ...?
        //
        // => assembly: the one we are currently weaving (MyGame.dll)
        // => resolver: useful in case we need to resolve any of the assembly's
        //              assembly.MainModule.AssemblyReferences.
        //              -> we can resolve ANY of them given that the resolver
        //                 works properly (need custom one for ILPostProcessor)
        //              -> IMPORTANT: .Resolve() takes an AssemblyNameReference.
        //                 those from assembly.MainModule.AssemblyReferences are
        //                 guaranteed to be resolve-able.
        //                 Parsing from a string for Library/.../Mirror.dll
        //                 would not be guaranteed to be resolve-able because
        //                 for ILPostProcessor we can't assume where Mirror.dll
        //                 is etc.
        public bool Weave(AssemblyDefinition assembly, IAssemblyResolver resolver, out bool modified)
        {
            WeavingFailed = false;
            modified = false;
            try
            {
                CurrentAssembly = assembly;

                // fix "No writer found for ..." error
                // https://github.com/vis2k/Mirror/issues/2579
                // -> when restarting Unity, weaver would try to weave a DLL
                //    again
                // -> resulting in two GeneratedNetworkCode classes (see ILSpy)
                // -> the second one wouldn't have all the writer types setup
                if (CurrentAssembly.MainModule.ContainsClass(GeneratedCodeNamespace, GeneratedCodeClassName))
                {
                    //Log.Warning($"Weaver: skipping {CurrentAssembly.Name} because already weaved");
                    return true;
                }

                weaverTypes = new WeaverTypes(CurrentAssembly, Log, ref WeavingFailed);

                // weaverTypes are needed for CreateGeneratedCodeClass
                CreateGeneratedCodeClass();

                // WeaverList depends on WeaverTypes setup because it uses Import
                syncVarAccessLists = new SyncVarAccessLists();

                // initialize readers & writers with this assembly.
                // we need to do this in every Process() call.
                // otherwise we would get
                // "System.ArgumentException: Member ... is declared in another module and needs to be imported"
                // errors when still using the previous module's reader/writer funcs.
                writers = new Writers(CurrentAssembly, weaverTypes, GeneratedCodeClass, Log);
                readers = new Readers(CurrentAssembly, weaverTypes, GeneratedCodeClass, Log);

                Stopwatch rwstopwatch = Stopwatch.StartNew();
                // Need to track modified from ReaderWriterProcessor too because it could find custom read/write functions or create functions for NetworkMessages
                modified = ReaderWriterProcessor.Process(CurrentAssembly, resolver, Log, writers, readers, ref WeavingFailed);
                rwstopwatch.Stop();
                Console.WriteLine($"Find all reader and writers took {rwstopwatch.ElapsedMilliseconds} milliseconds");

                ModuleDefinition moduleDefinition = CurrentAssembly.MainModule;
                Console.WriteLine($"Script Module: {moduleDefinition.Name}");

                modified |= WeaveModule(moduleDefinition);

                if (WeavingFailed)
                {
                    return false;
                }

                if (modified)
                {
                    SyncVarAttributeAccessReplacer.Process(Log, moduleDefinition, syncVarAccessLists);

                    // add class that holds read/write functions
                    moduleDefinition.Types.Add(GeneratedCodeClass);

                    ReaderWriterProcessor.InitializeReaderAndWriters(CurrentAssembly, weaverTypes, writers, readers, GeneratedCodeClass);

                    // DO NOT WRITE here.
                    // CompilationFinishedHook writes to the file.
                    // ILPostProcessor writes to in-memory assembly.
                    // it depends on the caller.
                    //CurrentAssembly.Write(new WriterParameters{ WriteSymbols = true });
                }

                // if weaving succeeded, switch on the Weaver Fuse in Mirror.dll
                if (CurrentAssembly.Name.Name == MirrorAssemblyName)
                {
                    ToggleWeaverFuse();
                }

                return true;
            }
            catch (Exception e)
            {
                Log.Error($"Exception :{e}");
                WeavingFailed = true;
                return false;
            }
        }
    }
}
