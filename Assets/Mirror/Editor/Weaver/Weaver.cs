using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    }

    internal static class Weaver
    {
        public static WeaverLists WeaveLists { get; private set; }
        public static AssemblyDefinition CurrentAssembly { get; private set; }
        public static ModuleDefinition CorLibModule { get; private set; }
        public static AssemblyDefinition UnityAssembly { get; private set; }
        public static AssemblyDefinition NetAssembly { get; private set; }
        public static bool WeavingFailed { get; private set; }
        public static bool GenerateLogErrors { get; set; }

        // private properties
        static readonly bool DebugLogEnabled = true;

        // Network types
        public static TypeReference NetworkBehaviourType;
        public static TypeReference RemoteCallHelperType;
        public static TypeReference MonoBehaviourType;
        public static TypeReference ScriptableObjectType;
        public static TypeReference NetworkConnectionType;

        public static TypeReference MessageBaseType;
        public static TypeReference IMessageBaseType;
        public static TypeReference SyncListType;
        public static TypeReference SyncSetType;
        public static TypeReference SyncDictionaryType;

        public static MethodReference ScriptableObjectCreateInstanceMethod;

        public static MethodReference NetworkBehaviourDirtyBitsReference;
        public static MethodReference GetPooledWriterReference;
        public static MethodReference RecycleWriterReference;
        public static TypeReference NetworkClientType;
        public static TypeReference NetworkServerType;

        public static TypeReference NetworkReaderType;

        public static TypeReference NetworkWriterType;
        public static TypeReference PooledNetworkWriterType;

        public static TypeReference NetworkIdentityType;
        public static TypeReference IEnumeratorType;

        public static TypeReference ClientSceneType;
        public static MethodReference ReadyConnectionReference;

        public static TypeReference ComponentType;
        public static TypeReference ObjectType;

        public static TypeReference CmdDelegateReference;
        public static MethodReference CmdDelegateConstructor;

        public static MethodReference NetworkServerGetActive;
        public static MethodReference NetworkServerGetLocalClientActive;
        public static MethodReference NetworkClientGetActive;

        // custom attribute types
        public static TypeReference SyncVarType;
        public static TypeReference CommandType;
        public static TypeReference ClientRpcType;
        public static TypeReference TargetRpcType;
        public static TypeReference SyncEventType;
        public static TypeReference SyncObjectType;
        public static MethodReference InitSyncObjectReference;

        // array segment
        public static TypeReference ArraySegmentType;
        public static MethodReference ArraySegmentConstructorReference;
        public static MethodReference ArraySegmentArrayReference;
        public static MethodReference ArraySegmentOffsetReference;
        public static MethodReference ArraySegmentCountReference;

        // system types
        public static TypeReference voidType;
        public static TypeReference singleType;
        public static TypeReference doubleType;
        public static TypeReference boolType;
        public static TypeReference int64Type;
        public static TypeReference uint64Type;
        public static TypeReference int32Type;
        public static TypeReference uint32Type;
        public static TypeReference objectType;
        public static TypeReference typeType;
        public static TypeReference gameObjectType;
        public static TypeReference transformType;

        public static MethodReference syncVarEqualReference;
        public static MethodReference syncVarNetworkIdentityEqualReference;
        public static MethodReference syncVarGameObjectEqualReference;
        public static MethodReference setSyncVarReference;
        public static MethodReference setSyncVarHookGuard;
        public static MethodReference getSyncVarHookGuard;
        public static MethodReference setSyncVarGameObjectReference;
        public static MethodReference getSyncVarGameObjectReference;
        public static MethodReference setSyncVarNetworkIdentityReference;
        public static MethodReference getSyncVarNetworkIdentityReference;
        public static MethodReference registerCommandDelegateReference;
        public static MethodReference registerRpcDelegateReference;
        public static MethodReference registerEventDelegateReference;
        public static MethodReference getTypeReference;
        public static MethodReference getTypeFromHandleReference;
        public static MethodReference logErrorReference;
        public static MethodReference logWarningReference;
        public static MethodReference sendCommandInternal;
        public static MethodReference sendRpcInternal;
        public static MethodReference sendTargetRpcInternal;
        public static MethodReference sendEventInternal;

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
                        objectType);

                const MethodAttributes methodAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
                MethodDefinition method = new MethodDefinition(".ctor", methodAttributes, voidType);
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Call, Resolvers.ResolveMethod(objectType, CurrentAssembly, ".ctor")));
                method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

                WeaveLists.generateContainerClass.Methods.Add(method);
            }
        }

        static bool ProcessNetworkBehaviourType(TypeDefinition td)
        {
            if (!NetworkBehaviourProcessor.WasProcessed(td))
            {
                DLog(td, "Found NetworkBehaviour " + td.FullName);

                NetworkBehaviourProcessor proc = new NetworkBehaviourProcessor(td);
                proc.Process();
                return true;
            }
            return false;
        }

        static void SetupUnityTypes()
        {
            gameObjectType = UnityAssembly.MainModule.GetType("UnityEngine.GameObject");
            transformType = UnityAssembly.MainModule.GetType("UnityEngine.Transform");

            NetworkClientType = NetAssembly.MainModule.GetType("Mirror.NetworkClient");
            NetworkServerType = NetAssembly.MainModule.GetType("Mirror.NetworkServer");

            SyncVarType = NetAssembly.MainModule.GetType("Mirror.SyncVarAttribute");
            CommandType = NetAssembly.MainModule.GetType("Mirror.CommandAttribute");
            ClientRpcType = NetAssembly.MainModule.GetType("Mirror.ClientRpcAttribute");
            TargetRpcType = NetAssembly.MainModule.GetType("Mirror.TargetRpcAttribute");
            SyncEventType = NetAssembly.MainModule.GetType("Mirror.SyncEventAttribute");
            SyncObjectType = NetAssembly.MainModule.GetType("Mirror.SyncObject");
        }

        static void SetupCorLib()
        {
            AssemblyNameReference name = AssemblyNameReference.Parse("mscorlib");
            ReaderParameters parameters = new ReaderParameters
            {
                AssemblyResolver = CurrentAssembly.MainModule.AssemblyResolver
            };
            CorLibModule = CurrentAssembly.MainModule.AssemblyResolver.Resolve(name, parameters).MainModule;
        }

        static TypeReference ImportCorLibType(string fullName)
        {
            TypeDefinition type = CorLibModule.GetType(fullName) ?? CorLibModule.ExportedTypes.First(t => t.FullName == fullName).Resolve();
            if (type != null)
            {
                return CurrentAssembly.MainModule.ImportReference(type);
            }
            Error("Failed to import mscorlib type: " + fullName + " because Resolve failed. (Might happen when trying to Resolve in NetStandard dll, see also: https://github.com/vis2k/Mirror/issues/791)");
            return null;
        }

        static void SetupTargetTypes()
        {
            // system types
            SetupCorLib();
            voidType = ImportCorLibType("System.Void");
            singleType = ImportCorLibType("System.Single");
            doubleType = ImportCorLibType("System.Double");
            boolType = ImportCorLibType("System.Boolean");
            int64Type = ImportCorLibType("System.Int64");
            uint64Type = ImportCorLibType("System.UInt64");
            int32Type = ImportCorLibType("System.Int32");
            uint32Type = ImportCorLibType("System.UInt32");
            objectType = ImportCorLibType("System.Object");
            typeType = ImportCorLibType("System.Type");
            IEnumeratorType = ImportCorLibType("System.Collections.IEnumerator");

            ArraySegmentType = ImportCorLibType("System.ArraySegment`1");
            ArraySegmentArrayReference = Resolvers.ResolveProperty(ArraySegmentType, CurrentAssembly, "Array");
            ArraySegmentCountReference = Resolvers.ResolveProperty(ArraySegmentType, CurrentAssembly, "Count");
            ArraySegmentOffsetReference = Resolvers.ResolveProperty(ArraySegmentType, CurrentAssembly, "Offset");
            ArraySegmentConstructorReference = Resolvers.ResolveMethod(ArraySegmentType, CurrentAssembly, ".ctor");

            NetworkReaderType = NetAssembly.MainModule.GetType("Mirror.NetworkReader");
            NetworkWriterType = NetAssembly.MainModule.GetType("Mirror.NetworkWriter");
            TypeReference pooledNetworkWriterTmp = NetAssembly.MainModule.GetType("Mirror.PooledNetworkWriter");
            PooledNetworkWriterType = CurrentAssembly.MainModule.ImportReference(pooledNetworkWriterTmp);

            NetworkServerGetActive = Resolvers.ResolveMethod(NetworkServerType, CurrentAssembly, "get_active");
            NetworkServerGetLocalClientActive = Resolvers.ResolveMethod(NetworkServerType, CurrentAssembly, "get_localClientActive");
            NetworkClientGetActive = Resolvers.ResolveMethod(NetworkClientType, CurrentAssembly, "get_active");

            CmdDelegateReference = NetAssembly.MainModule.GetType("Mirror.RemoteCalls.CmdDelegate");
            CmdDelegateConstructor = Resolvers.ResolveMethod(CmdDelegateReference, CurrentAssembly, ".ctor");

            CurrentAssembly.MainModule.ImportReference(gameObjectType);
            CurrentAssembly.MainModule.ImportReference(transformType);

            TypeReference networkIdentityTmp = NetAssembly.MainModule.GetType("Mirror.NetworkIdentity");
            NetworkIdentityType = CurrentAssembly.MainModule.ImportReference(networkIdentityTmp);

            NetworkBehaviourType = NetAssembly.MainModule.GetType("Mirror.NetworkBehaviour");
            RemoteCallHelperType = NetAssembly.MainModule.GetType("Mirror.RemoteCalls.RemoteCallHelper");
            NetworkConnectionType = NetAssembly.MainModule.GetType("Mirror.NetworkConnection");

            MonoBehaviourType = UnityAssembly.MainModule.GetType("UnityEngine.MonoBehaviour");
            ScriptableObjectType = UnityAssembly.MainModule.GetType("UnityEngine.ScriptableObject");

            ScriptableObjectCreateInstanceMethod = Resolvers.ResolveMethod(
                ScriptableObjectType, CurrentAssembly,
                md => md.Name == "CreateInstance" && md.HasGenericParameters);

            NetworkConnectionType = NetAssembly.MainModule.GetType("Mirror.NetworkConnection");
            NetworkConnectionType = CurrentAssembly.MainModule.ImportReference(NetworkConnectionType);

            MessageBaseType = NetAssembly.MainModule.GetType("Mirror.MessageBase");
            IMessageBaseType = NetAssembly.MainModule.GetType("Mirror.IMessageBase");
            SyncListType = NetAssembly.MainModule.GetType("Mirror.SyncList`1");
            SyncSetType = NetAssembly.MainModule.GetType("Mirror.SyncSet`1");
            SyncDictionaryType = NetAssembly.MainModule.GetType("Mirror.SyncDictionary`2");

            NetworkBehaviourDirtyBitsReference = Resolvers.ResolveProperty(NetworkBehaviourType, CurrentAssembly, "syncVarDirtyBits");
            TypeDefinition NetworkWriterPoolType = NetAssembly.MainModule.GetType("Mirror.NetworkWriterPool");
            GetPooledWriterReference = Resolvers.ResolveMethod(NetworkWriterPoolType, CurrentAssembly, "GetWriter");
            RecycleWriterReference = Resolvers.ResolveMethod(NetworkWriterPoolType, CurrentAssembly, "Recycle");

            ComponentType = UnityAssembly.MainModule.GetType("UnityEngine.Component");
            ObjectType = UnityAssembly.MainModule.GetType("UnityEngine.Object");
            ClientSceneType = NetAssembly.MainModule.GetType("Mirror.ClientScene");
            ReadyConnectionReference = Resolvers.ResolveMethod(ClientSceneType, CurrentAssembly, "get_readyConnection");

            syncVarEqualReference = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "SyncVarEqual");
            syncVarNetworkIdentityEqualReference = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "SyncVarNetworkIdentityEqual");
            syncVarGameObjectEqualReference = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "SyncVarGameObjectEqual");
            setSyncVarReference = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "SetSyncVar");
            setSyncVarHookGuard = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "setSyncVarHookGuard");
            getSyncVarHookGuard = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "getSyncVarHookGuard");

            setSyncVarGameObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "SetSyncVarGameObject");
            getSyncVarGameObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "GetSyncVarGameObject");
            setSyncVarNetworkIdentityReference = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "SetSyncVarNetworkIdentity");
            getSyncVarNetworkIdentityReference = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "GetSyncVarNetworkIdentity");
            registerCommandDelegateReference = Resolvers.ResolveMethod(RemoteCallHelperType, CurrentAssembly, "RegisterCommandDelegate");
            registerRpcDelegateReference = Resolvers.ResolveMethod(RemoteCallHelperType, CurrentAssembly, "RegisterRpcDelegate");
            registerEventDelegateReference = Resolvers.ResolveMethod(RemoteCallHelperType, CurrentAssembly, "RegisterEventDelegate");
            getTypeReference = Resolvers.ResolveMethod(objectType, CurrentAssembly, "GetType");
            getTypeFromHandleReference = Resolvers.ResolveMethod(typeType, CurrentAssembly, "GetTypeFromHandle");
            logErrorReference = Resolvers.ResolveMethod(UnityAssembly.MainModule.GetType("UnityEngine.Debug"), CurrentAssembly, "LogError");
            logWarningReference = Resolvers.ResolveMethod(UnityAssembly.MainModule.GetType("UnityEngine.Debug"), CurrentAssembly, "LogWarning");
            sendCommandInternal = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "SendCommandInternal");
            sendRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "SendRPCInternal");
            sendTargetRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "SendTargetRPCInternal");
            sendEventInternal = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "SendEventInternal");

            SyncObjectType = CurrentAssembly.MainModule.ImportReference(SyncObjectType);
            InitSyncObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, CurrentAssembly, "InitSyncObject");
        }

        public static bool IsNetworkBehaviour(TypeDefinition td)
        {
            return td.IsDerivedFrom(NetworkBehaviourType);
        }

        static void CheckMonoBehaviour(TypeDefinition td)
        {
            if (td.IsDerivedFrom(MonoBehaviourType))
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
                if (parent.FullName == NetworkBehaviourType.FullName)
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
                modified |= ProcessNetworkBehaviourType(behaviour);
            }
            return modified;
        }

        static bool WeaveMessage(TypeDefinition td)
        {
            if (!td.IsClass)
                return false;

            bool modified = false;

            if (td.ImplementsInterface(IMessageBaseType))
            {
                MessageClassProcessor.Process(td);
                modified = true;
            }

            // check for embedded types
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
                if (td.IsDerivedFrom(SyncListType))
                {
                    SyncListProcessor.Process(td, SyncListType);
                    modified = true;
                }
                else if (td.IsDerivedFrom(SyncSetType))
                {
                    SyncListProcessor.Process(td, SyncSetType);
                    modified = true;
                }
                else if (td.IsDerivedFrom(SyncDictionaryType))
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

        static bool Weave(string assName, IEnumerable<string> dependencies, string unityEngineDLLPath, string mirrorNetDLLPath, string outputDir)
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

                SetupTargetTypes();
                System.Diagnostics.Stopwatch rwstopwatch = System.Diagnostics.Stopwatch.StartNew();
                ReaderWriterProcessor.ProcessReadersAndWriters(CurrentAssembly);
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
                        PropertySiteProcessor.ProcessSitesModule(CurrentAssembly.MainModule);
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
                    if (outputDir != null)
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
                    }
                }
                watch.Stop();
                Console.WriteLine("Weave behaviours and messages took" + watch.ElapsedMilliseconds + " milliseconds");

                return modified;
            }
            catch (Exception ex)
            {
                Error(ex.ToString());
                throw ex;
            }
        }

        public static bool WeaveAssemblies(IEnumerable<string> assemblies, IEnumerable<string> dependencies, string outputDir, string unityEngineDLLPath, string mirrorNetDLLPath)
        {
            WeavingFailed = false;
            WeaveLists = new WeaverLists();

            using (UnityAssembly = AssemblyDefinition.ReadAssembly(unityEngineDLLPath))
            using (NetAssembly = AssemblyDefinition.ReadAssembly(mirrorNetDLLPath))
            {
                SetupUnityTypes();

                try
                {
                    foreach (string ass in assemblies)
                    {
                        if (!Weave(ass, dependencies, unityEngineDLLPath, mirrorNetDLLPath, outputDir))
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
    }
}
