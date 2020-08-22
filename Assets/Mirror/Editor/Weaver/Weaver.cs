using System;
using System.Collections.Generic;
using System.IO;
using Mono.CecilX;
using Mono.CecilX.Cil;

namespace Mirror.Weaver
{
    // This data is flushed each time - if we are run multiple times in the same process/domain
    class WeaverLists
    {
        // setter functions that replace [SyncVar] member variable references. dict<field, replacement>
        public Dictionary<FieldDefinition, MethodDefinition> replacementSetterProperties = new Dictionary<FieldDefinition, MethodDefinition>();
        // getter functions that replace [SyncVar] member variable references. dict<field, replacement>
        public Dictionary<FieldDefinition, MethodDefinition> replacementGetterProperties = new Dictionary<FieldDefinition, MethodDefinition>();

        // [SyncEvent] invoke functions that should be replaced. dict<originalEventName, replacement>
        public Dictionary<string, MethodDefinition> replaceEvents = new Dictionary<string, MethodDefinition>();

        public List<MethodDefinition> generatedReadFunctions = new List<MethodDefinition>();
        public List<MethodDefinition> generatedWriteFunctions = new List<MethodDefinition>();

        public TypeDefinition generateContainerClass;

        // amount of SyncVars per class. dict<className, amount>
        public Dictionary<string, int> numSyncVars = new Dictionary<string, int>();

        public HashSet<string> ProcessedMessages = new HashSet<string>();
    }

    internal static class Weaver
    {
        public static string InvokeRpcPrefix => "InvokeUserCode_";
        public static string SyncEventPrefix => "SendEventMessage_";

        public static WeaverLists WeaveLists { get; private set; }
        public static AssemblyDefinition CurrentAssembly { get; private set; }
        public static bool WeavingFailed { get; private set; }
        public static bool GenerateLogErrors { get; set; }

        // private properties
        static readonly bool DebugLogEnabled = true;

        public static void DLog(TypeDefinition td, string fmt, params object[] args)
        {
            if (!DebugLogEnabled)
                return;

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
                        WeaverTypes.objectType);

                const MethodAttributes methodAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
                MethodDefinition method = new MethodDefinition(".ctor", methodAttributes, WeaverTypes.voidType);
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Call, Resolvers.ResolveMethod(WeaverTypes.objectType, CurrentAssembly, ".ctor")));
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

                WeaveLists.generateContainerClass.Methods.Add(method);
            }
        }

        public static bool IsNetworkBehaviour(TypeDefinition td)
        {
            return td.IsDerivedFrom(WeaverTypes.NetworkBehaviourType);
        }

        static void CheckMonoBehaviour(TypeDefinition td)
        {
            if (td.IsDerivedFrom(WeaverTypes.MonoBehaviourType))
            {
                MonoBehaviourProcessor.Process(td);
            }
        }

        static bool WeaveNetworkBehavior(TypeDefinition td)
        {
            if (!td.IsClass)
                return false;

            if (!IsNetworkBehaviour(td))
            {
                CheckMonoBehaviour(td);
                return false;
            }

            // process this and base classes from parent to child order

            List<TypeDefinition> behaviourClasses = new List<TypeDefinition>();

            TypeDefinition parent = td;
            while (parent != null)
            {
                if (parent.FullName == WeaverTypes.NetworkBehaviourType.FullName)
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
                modified |= new NetworkBehaviourProcessor(behaviour).Process();
            }
            return modified;
        }

        static bool WeaveMessage(TypeDefinition td)
        {
            if (!td.IsClass)
                return false;

            // already processed
            if (WeaveLists.ProcessedMessages.Contains(td.FullName))
                return false;

            bool modified = false;

            if (td.ImplementsInterface(WeaverTypes.IMessageBaseType))
            {
                // process this and base classes from parent to child order
                try
                {
                    TypeDefinition parent = td.BaseType.Resolve();
                    // process parent
                    WeaveMessage(parent);
                }
                catch (AssemblyResolutionException)
                {
                    // this can happen for plugins.
                    //Console.WriteLine("AssemblyResolutionException: "+ ex.ToString());
                }

                // process this
                MessageClassProcessor.Process(td);
                WeaveLists.ProcessedMessages.Add(td.FullName);
                modified = true;
            }

            // check for embedded types
            // inner classes should be processed after outter class to avoid StackOverflowException
            foreach (TypeDefinition embedded in td.NestedTypes)
            {
                modified |= WeaveMessage(embedded);
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
                if (td.IsDerivedFrom(WeaverTypes.SyncListType))
                {
                    SyncListProcessor.Process(td, WeaverTypes.SyncListType);
                    modified = true;
                }
                else if (td.IsDerivedFrom(WeaverTypes.SyncSetType))
                {
                    SyncListProcessor.Process(td, WeaverTypes.SyncSetType);
                    modified = true;
                }
                else if (td.IsDerivedFrom(WeaverTypes.SyncDictionaryType))
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
                System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
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
                        modified |= WeaveMessage(td);
                        modified |= ServerClientAttributeProcessor.Process(td);
                    }
                }
                watch.Stop();
                Console.WriteLine("Weave behaviours and messages took" + watch.ElapsedMilliseconds + " milliseconds");

                return modified;
            }
            catch (Exception ex)
            {
                Error(ex.ToString());
                throw new Exception(ex.Message, ex);
            }
        }

        static bool Weave(string assName, AssemblyDefinition unityAssembly, AssemblyDefinition mirrorAssembly, IEnumerable<string> dependencies, string unityEngineDLLPath, string mirrorNetDLLPath, string outputDir)
        {
            using (DefaultAssemblyResolver asmResolver = new DefaultAssemblyResolver())
            using (CurrentAssembly = AssemblyDefinition.ReadAssembly(assName, new ReaderParameters { ReadWrite = true, ReadSymbols = true, AssemblyResolver = asmResolver }))
            {
                asmResolver.AddSearchDirectory(Path.GetDirectoryName(assName));
                asmResolver.AddSearchDirectory(Helpers.UnityEngineDllDirectoryName());
                asmResolver.AddSearchDirectory(Path.GetDirectoryName(unityEngineDLLPath));
                asmResolver.AddSearchDirectory(Path.GetDirectoryName(mirrorNetDLLPath));
                if (dependencies != null)
                {
                    foreach (string path in dependencies)
                    {
                        asmResolver.AddSearchDirectory(path);
                    }
                }

                WeaverTypes.SetupTargetTypes(unityAssembly, mirrorAssembly, CurrentAssembly);
                System.Diagnostics.Stopwatch rwstopwatch = System.Diagnostics.Stopwatch.StartNew();
                ReaderWriterProcessor.Process(CurrentAssembly);
                rwstopwatch.Stop();
                Console.WriteLine("Find all reader and writers took " + rwstopwatch.ElapsedMilliseconds + " milliseconds");

                ModuleDefinition moduleDefinition = CurrentAssembly.MainModule;
                Console.WriteLine("Script Module: {0}", moduleDefinition.Name);

                bool modified = WeaveModule(moduleDefinition);

                if (WeavingFailed)
                {
                    return false;
                }

                if (modified)
                {
                    // this must be done for ALL code, not just NetworkBehaviours
                    try
                    {
                        PropertySiteProcessor.Process(moduleDefinition);
                    }
                    catch (Exception e)
                    {
                        Log.Error("ProcessPropertySites exception: " + e);
                        return false;
                    }

                    if (WeavingFailed)
                    {
                        return false;
                    }

                    // write to outputDir if specified, otherwise perform in-place write
                    WriterParameters writeParams = new WriterParameters { WriteSymbols = true };
                    if (!string.IsNullOrEmpty(outputDir))
                    {
                        CurrentAssembly.Write(Helpers.DestinationFileFor(outputDir, assName), writeParams);
                    }
                    else
                    {
                        CurrentAssembly.Write(writeParams);
                    }
                }
            }

            return true;
        }

        static bool WeaveAssemblies(IEnumerable<string> assemblies, IEnumerable<string> dependencies, string outputDir, string unityEngineDLLPath, string mirrorNetDLLPath)
        {
            WeavingFailed = false;
            WeaveLists = new WeaverLists();

            using (AssemblyDefinition unityAssembly = AssemblyDefinition.ReadAssembly(unityEngineDLLPath))
            using (AssemblyDefinition mirrorAssembly = AssemblyDefinition.ReadAssembly(mirrorNetDLLPath))
            {
                WeaverTypes.SetupUnityTypes(unityAssembly, mirrorAssembly);

                try
                {
                    foreach (string asm in assemblies)
                    {
                        if (!Weave(asm, unityAssembly, mirrorAssembly, dependencies, unityEngineDLLPath, mirrorNetDLLPath, outputDir))
                        {
                            return false;
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error("Exception :" + e);
                    return false;
                }
            }
            return true;
        }


        public static bool Process(string unityEngine, string netDLL, string outputDirectory, string[] assemblies, string[] extraAssemblyPaths, Action<string> printWarning, Action<string> printError)
        {
            Validate(unityEngine, netDLL, outputDirectory, assemblies, extraAssemblyPaths);
            Log.WarningMethod = printWarning;
            Log.ErrorMethod = printError;
            return WeaveAssemblies(assemblies, extraAssemblyPaths, outputDirectory, unityEngine, netDLL);
        }

        static void Validate(string unityEngine, string netDLL, string outputDirectory, string[] assemblies, string[] extraAssemblyPaths)
        {
            CheckDllPath(unityEngine);
            CheckDllPath(netDLL);
            CheckOutputDirectory(outputDirectory);
            CheckAssemblies(assemblies);
        }
        static void CheckDllPath(string path)
        {
            if (!File.Exists(path))
                throw new Exception("dll could not be located at " + path + "!");
        }
        static void CheckAssemblies(IEnumerable<string> assemblyPaths)
        {
            foreach (string assemblyPath in assemblyPaths)
                CheckAssemblyPath(assemblyPath);
        }
        static void CheckAssemblyPath(string assemblyPath)
        {
            if (!File.Exists(assemblyPath))
                throw new Exception("Assembly " + assemblyPath + " does not exist!");
        }
        static void CheckOutputDirectory(string outputDir)
        {
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
        }
    }
}
