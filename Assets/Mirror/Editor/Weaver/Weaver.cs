using System;
using System.Collections.Generic;
using System.Linq;
using Mono.CecilX;

namespace Mirror.Weaver
{
    internal static class Weaver
    {
        public const string InvokeRpcPrefix = "InvokeUserCode_";

        // generated code class
        public const string GeneratedCodeNamespace = "Mirror";
        public const string GeneratedCodeClassName = "GeneratedNetworkCode";
        public static TypeDefinition GeneratedCodeClass;

        public static WeaverTypes weaverTypes;
        public static WeaverLists weaverLists { get; private set; }
        public static AssemblyDefinition CurrentAssembly { get; private set; }
        public static bool WeavingFailed;

        // logger functions can be set from the outside.
        // for example, Debug.Log or ILPostProcessor Diagnostics log for
        // multi threaded logging.
        public static Logger Log;

        static bool WeaveNetworkBehavior(TypeDefinition td)
        {
            if (!td.IsClass)
                return false;

            if (!td.IsDerivedFrom<NetworkBehaviour>())
            {
                if (td.IsDerivedFrom<UnityEngine.MonoBehaviour>())
                    MonoBehaviourProcessor.Process(td);
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
                modified |= new NetworkBehaviourProcessor(CurrentAssembly, weaverTypes, weaverLists, Log, behaviour).Process();
            }
            return modified;
        }

        static bool WeaveModule(ModuleDefinition moduleDefinition)
        {
            try
            {
                bool modified = false;

                System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();

                watch.Start();
                foreach (TypeDefinition td in moduleDefinition.Types)
                {
                    if (td.IsClass && td.BaseType.CanBeResolved())
                    {
                        modified |= WeaveNetworkBehavior(td);
                        modified |= ServerClientAttributeProcessor.Process(weaverTypes, td);
                    }
                }
                watch.Stop();
                Console.WriteLine("Weave behaviours and messages took " + watch.ElapsedMilliseconds + " milliseconds");

                return modified;
            }
            catch (Exception ex)
            {

                throw new Exception(ex.Message, ex);
            }
        }

        static void CreateGeneratedCodeClass()
        {
            // create "Mirror.GeneratedNetworkCode" class
            GeneratedCodeClass = new TypeDefinition(GeneratedCodeNamespace, GeneratedCodeClassName,
                TypeAttributes.BeforeFieldInit | TypeAttributes.Class | TypeAttributes.AnsiClass | TypeAttributes.Public | TypeAttributes.AutoClass | TypeAttributes.Abstract | TypeAttributes.Sealed,
                weaverTypes.Import<object>());
        }

        static bool ContainsGeneratedCodeClass(ModuleDefinition module)
        {
            return module.GetTypes().Any(td => td.Namespace == GeneratedCodeNamespace &&
                                               td.Name == GeneratedCodeClassName);
        }

        // Weave takes an AssemblyDefinition to be compatible with both old and
        // new weavers:
        // * old takes a filepath, new takes a in-memory byte[]
        // * old uses DefaultAssemblyResolver with added dependencies paths,
        //   new uses ...?
        public static bool Weave(AssemblyDefinition asmDef)
        {
            WeavingFailed = false;
            try
            {
                CurrentAssembly = asmDef;

                // fix "No writer found for ..." error
                // https://github.com/vis2k/Mirror/issues/2579
                // -> when restarting Unity, weaver would try to weave a DLL
                //    again
                // -> resulting in two GeneratedNetworkCode classes (see ILSpy)
                // -> the second one wouldn't have all the writer types setup
                if (ContainsGeneratedCodeClass(CurrentAssembly.MainModule))
                {
                    //Log.Warning($"Weaver: skipping {CurrentAssembly.Name} because already weaved");
                    return true;
                }

                weaverTypes = new WeaverTypes(CurrentAssembly);

                CreateGeneratedCodeClass();

                // WeaverList depends on WeaverTypes setup because it uses Import
                weaverLists = new WeaverLists();

                System.Diagnostics.Stopwatch rwstopwatch = System.Diagnostics.Stopwatch.StartNew();
                // Need to track modified from ReaderWriterProcessor too because it could find custom read/write functions or create functions for NetworkMessages
                bool modified = ReaderWriterProcessor.Process(CurrentAssembly, weaverTypes, Log);
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
                    PropertySiteProcessor.Process(moduleDefinition, weaverLists);

                    // add class that holds read/write functions
                    moduleDefinition.Types.Add(GeneratedCodeClass);

                    ReaderWriterProcessor.InitializeReaderAndWriters(CurrentAssembly, weaverTypes);

                    // write to outputDir if specified, otherwise perform in-place write
                    WriterParameters writeParams = new WriterParameters { WriteSymbols = true };
                    CurrentAssembly.Write(writeParams);
                }

                return true;
            }
            catch (Exception e)
            {
                Log.Error("Exception :" + e);
                WeavingFailed = true;
                return false;
            }
        }
    }
}
