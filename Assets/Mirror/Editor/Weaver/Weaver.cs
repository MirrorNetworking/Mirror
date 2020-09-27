using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEditor.Compilation;

namespace Mirror.Weaver
{
    // This data is flushed each time - if we are run multiple times in the same process/domain
    class WeaverLists
    {
        // setter functions that replace [SyncVar] member variable references. dict<field, replacement>
        public Dictionary<FieldDefinition, MethodDefinition> replacementSetterProperties = new Dictionary<FieldDefinition, MethodDefinition>();
        // getter functions that replace [SyncVar] member variable references. dict<field, replacement>
        public Dictionary<FieldDefinition, MethodDefinition> replacementGetterProperties = new Dictionary<FieldDefinition, MethodDefinition>();

        public List<MethodDefinition> generatedReadFunctions = new List<MethodDefinition>();
        public List<MethodDefinition> generatedWriteFunctions = new List<MethodDefinition>();

        public TypeDefinition generateContainerClass;

        // amount of SyncVars per class. dict<className, amount>
        public Dictionary<string, int> numSyncVars = new Dictionary<string, int>();
    }

    internal static class Weaver
    {
        public static string InvokeRpcPrefix => "InvokeUserCode_";

        public static WeaverLists WeaveLists { get; private set; }
        public static AssemblyDefinition CurrentAssembly { get; private set; }
        public static bool WeavingFailed { get; private set; }

        public static void DLog(TypeDefinition td, string fmt, params object[] args)
        {
            Console.WriteLine("[" + td.Name + "] " + string.Format(fmt, args));
        }

        // display weaver error
        // and mark process as failed
        public static void Error(string message)
        {
            Log.Error(message);
            WeavingFailed = true;
        }

        public static void Error(string message, MemberReference mr)
        {
            Log.Error($"{message} (at {mr})");
            WeavingFailed = true;
        }

        public static void Warning(string message, MemberReference mr)
        {
            Log.Warning($"{message} (at {mr})");
        }

        public static int GetSyncVarStart(string className)
        {
            return WeaveLists.numSyncVars.ContainsKey(className)
                   ? WeaveLists.numSyncVars[className]
                   : 0;
        }

        public static void SetNumSyncVars(string className, int num)
        {
            WeaveLists.numSyncVars[className] = num;
        }

        internal static void ConfirmGeneratedCodeClass()
        {
            if (WeaveLists.generateContainerClass == null)
            {
                WeaveLists.generateContainerClass = new TypeDefinition("Mirror", "GeneratedNetworkCode",
                        TypeAttributes.BeforeFieldInit | TypeAttributes.Class | TypeAttributes.AnsiClass | TypeAttributes.Public | TypeAttributes.AutoClass,
                        WeaverTypes.Import<object>());

                const MethodAttributes methodAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
                var method = new MethodDefinition(".ctor", methodAttributes, WeaverTypes.Import(typeof(void)));
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Call, Resolvers.ResolveMethod(WeaverTypes.Import<object>(), CurrentAssembly, ".ctor")));
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

                WeaveLists.generateContainerClass.Methods.Add(method);
            }
        }

        static void CheckMonoBehaviour(TypeDefinition td)
        {
            if (td.IsDerivedFrom<UnityEngine.MonoBehaviour>())
            {
                MonoBehaviourProcessor.Process(td);
            }
        }

        static bool WeaveNetworkBehavior(TypeDefinition td)
        {
            if (!td.IsClass)
                return false;

            if (!td.IsDerivedFrom<NetworkBehaviour>())
            {
                CheckMonoBehaviour(td);
                return false;
            }

            // process this and base classes from parent to child order

            var behaviourClasses = new List<TypeDefinition>();

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
                    break;
                }
            }

            bool modified = false;
            foreach (TypeDefinition behaviour in behaviourClasses)
            {
                modified |= new NetworkBehaviourProcessor(behaviour).Process();
            }
            return modified;
        }

        static bool WeaveSyncObject(TypeDefinition td)
        {
            bool modified = false;

            // ignore generic classes
            // we can not process generic classes
            // we give error if a generic syncObject is used in NetworkBehaviour
            if (td.HasGenericParameters)
                return false;

            // ignore abstract classes
            // we dont need to process abstract classes because classes that
            // inherit from them will be processed instead

            // We cant early return with non classes or Abstract classes
            // because we still need to check for embeded types
            if (td.IsClass || !td.IsAbstract)
            {
                if (td.IsDerivedFrom(typeof(SyncList<>)))
                {
                    SyncListProcessor.Process(td, WeaverTypes.Import(typeof(SyncList<>)));
                    modified = true;
                }
                else if (td.IsDerivedFrom(typeof(SyncSet<>)))
                {
                    SyncListProcessor.Process(td, WeaverTypes.Import(typeof(SyncSet<>)));
                    modified = true;
                }
                else if (td.IsDerivedFrom(typeof(SyncDictionary<,>)))
                {
                    SyncDictionaryProcessor.Process(td);
                    modified = true;
                }
            }

            // check for embedded types
            foreach (TypeDefinition embedded in td.NestedTypes)
            {
                modified |= WeaveSyncObject(embedded);
            }

            return modified;
        }

        static bool WeaveModule(ModuleDefinition moduleDefinition)
        {
            try
            {
                bool modified = false;

                // We need to do 2 passes, because SyncListStructs might be referenced from other modules, so we must make sure we generate them first.
                var watch = System.Diagnostics.Stopwatch.StartNew();
                foreach (TypeDefinition td in moduleDefinition.Types)
                {
                    if (td.IsClass && td.BaseType.CanBeResolved())
                    {
                        modified |= WeaveSyncObject(td);
                    }
                }
                watch.Stop();
                Console.WriteLine("Weave sync objects took " + watch.ElapsedMilliseconds + " milliseconds");

                watch.Start();
                foreach (TypeDefinition td in moduleDefinition.Types)
                {
                    if (td.IsClass && td.BaseType.CanBeResolved())
                    {
                        modified |= WeaveNetworkBehavior(td);
                        modified |= ServerClientAttributeProcessor.Process(td);
                    }
                }
                watch.Stop();
                Console.WriteLine("Weave behaviours and messages took" + watch.ElapsedMilliseconds + " milliseconds");

                if (modified)
                    PropertySiteProcessor.Process(moduleDefinition);

                return modified;
            }
            catch (Exception ex)
            {
                Error(ex.ToString());
                throw new Exception(ex.Message, ex);
            }
        }

        static bool Weave(Assembly unityAssembly)
        {
            using (var asmResolver = new DefaultAssemblyResolver())
            using (CurrentAssembly = AssemblyDefinition.ReadAssembly(unityAssembly.outputPath, new ReaderParameters { ReadWrite = true, ReadSymbols = true, AssemblyResolver = asmResolver }))
            {
                AddPaths(asmResolver, unityAssembly);

                WeaverTypes.SetupTargetTypes(CurrentAssembly);
                var rwstopwatch = System.Diagnostics.Stopwatch.StartNew();
                ReaderWriterProcessor.Process(CurrentAssembly, unityAssembly);
                rwstopwatch.Stop();
                Console.WriteLine($"Find all reader and writers took {rwstopwatch.ElapsedMilliseconds} milliseconds");

                ModuleDefinition moduleDefinition = CurrentAssembly.MainModule;
                Console.WriteLine($"Script Module: {moduleDefinition.Name}");

                bool modified = WeaveModule(moduleDefinition);

                if (WeavingFailed)
                {
                    return false;
                }

                if (modified)
                {
                    ReaderWriterProcessor.GenerateRWRegister(CurrentAssembly);

                    // write to outputDir if specified, otherwise perform in-place write
                    var writeParams = new WriterParameters { WriteSymbols = true };
                    CurrentAssembly.Write(writeParams);
                }
            }

            return true;
        }

        private static void AddPaths(DefaultAssemblyResolver asmResolver, Assembly assembly)
        {
            foreach (string path in assembly.allReferences)
            {
                asmResolver.AddSearchDirectory(Path.GetDirectoryName(path));
            }
    }

        public static bool WeaveAssembly(Assembly assembly)
        {
            WeavingFailed = false;
            WeaveLists = new WeaverLists();

            try
            {
                return Weave(assembly);
            }
            catch (Exception e)
            {
                Log.Error("Exception :" + e);
                return false;
            }
        }

    }
}
