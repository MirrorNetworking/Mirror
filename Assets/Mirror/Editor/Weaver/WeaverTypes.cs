using System.Linq;
using Mono.CecilX;

namespace Mirror.Weaver
{
    public static class WeaverTypes
    {
        // Network types
        public static TypeReference NetworkBehaviourType { get; private set; }
        public static TypeReference RemoteCallHelperType { get; private set; }
        public static TypeReference MonoBehaviourType { get; private set; }
        public static TypeReference ScriptableObjectType { get; private set; }
        public static TypeReference NetworkConnectionType { get; private set; }

        public static TypeReference MessageBaseType { get; private set; }
        public static TypeReference IMessageBaseType { get; private set; }
        public static TypeReference SyncListType { get; private set; }
        public static TypeReference SyncSetType { get; private set; }
        public static TypeReference SyncDictionaryType { get; private set; }

        public static MethodReference ScriptableObjectCreateInstanceMethod { get; private set; }

        public static MethodReference NetworkBehaviourDirtyBitsReference { get; private set; }
        public static MethodReference GetPooledWriterReference { get; private set; }
        public static MethodReference RecycleWriterReference { get; private set; }
        public static TypeReference NetworkClientType { get; private set; }
        public static TypeReference NetworkServerType { get; private set; }

        public static TypeReference NetworkReaderType { get; private set; }

        public static TypeReference NetworkWriterType { get; private set; }
        public static TypeReference PooledNetworkWriterType { get; private set; }

        public static TypeReference NetworkIdentityType { get; private set; }
        public static TypeReference IEnumeratorType { get; private set; }

        public static TypeReference ClientSceneType { get; private set; }
        public static MethodReference ReadyConnectionReference { get; private set; }

        public static TypeReference ComponentType { get; private set; }
        public static TypeReference ObjectType { get; private set; }

        public static TypeReference CmdDelegateReference { get; private set; }
        public static MethodReference CmdDelegateConstructor { get; private set; }

        public static MethodReference NetworkServerGetActive { get; private set; }
        public static MethodReference NetworkServerGetLocalClientActive { get; private set; }
        public static MethodReference NetworkClientGetActive { get; private set; }

        // custom attribute types
        public static TypeReference SyncVarType { get; private set; }
        public static TypeReference CommandType { get; private set; }
        public static TypeReference ClientRpcType { get; private set; }
        public static TypeReference TargetRpcType { get; private set; }
        public static TypeReference SyncEventType { get; private set; }
        public static TypeReference SyncObjectType { get; private set; }
        public static MethodReference InitSyncObjectReference { get; private set; }

        // array segment
        public static TypeReference ArraySegmentType { get; private set; }
        public static MethodReference ArraySegmentConstructorReference { get; private set; }
        public static MethodReference ArraySegmentArrayReference { get; private set; }
        public static MethodReference ArraySegmentOffsetReference { get; private set; }
        public static MethodReference ArraySegmentCountReference { get; private set; }

        // system types
        public static TypeReference voidType { get; private set; }
        public static TypeReference singleType { get; private set; }
        public static TypeReference doubleType { get; private set; }
        public static TypeReference boolType { get; private set; }
        public static TypeReference int64Type { get; private set; }
        public static TypeReference uint64Type { get; private set; }
        public static TypeReference int32Type { get; private set; }
        public static TypeReference uint32Type { get; private set; }
        public static TypeReference objectType { get; private set; }
        public static TypeReference typeType { get; private set; }
        public static TypeReference gameObjectType { get; private set; }
        public static TypeReference transformType { get; private set; }

        public static MethodReference syncVarEqualReference { get; private set; }
        public static MethodReference syncVarNetworkIdentityEqualReference { get; private set; }
        public static MethodReference syncVarGameObjectEqualReference { get; private set; }
        public static MethodReference setSyncVarReference { get; private set; }
        public static MethodReference setSyncVarHookGuard { get; private set; }
        public static MethodReference getSyncVarHookGuard { get; private set; }
        public static MethodReference setSyncVarGameObjectReference { get; private set; }
        public static MethodReference getSyncVarGameObjectReference { get; private set; }
        public static MethodReference setSyncVarNetworkIdentityReference { get; private set; }
        public static MethodReference getSyncVarNetworkIdentityReference { get; private set; }
        public static MethodReference registerCommandDelegateReference { get; private set; }
        public static MethodReference registerRpcDelegateReference { get; private set; }
        public static MethodReference registerEventDelegateReference { get; private set; }
        public static MethodReference getTypeReference { get; private set; }
        public static MethodReference getTypeFromHandleReference { get; private set; }
        public static MethodReference logErrorReference { get; private set; }
        public static MethodReference logWarningReference { get; private set; }
        public static MethodReference sendCommandInternal { get; private set; }
        public static MethodReference sendRpcInternal { get; private set; }
        public static MethodReference sendTargetRpcInternal { get; private set; }
        public static MethodReference sendEventInternal { get; private set; }


        public static void SetupUnityTypes(AssemblyDefinition unityAssembly, AssemblyDefinition mirrorAssembly)
        {
            gameObjectType = unityAssembly.MainModule.GetType("UnityEngine.GameObject");
            transformType = unityAssembly.MainModule.GetType("UnityEngine.Transform");

            NetworkClientType = mirrorAssembly.MainModule.GetType("Mirror.NetworkClient");
            NetworkServerType = mirrorAssembly.MainModule.GetType("Mirror.NetworkServer");

            SyncVarType = mirrorAssembly.MainModule.GetType("Mirror.SyncVarAttribute");
            CommandType = mirrorAssembly.MainModule.GetType("Mirror.CommandAttribute");
            ClientRpcType = mirrorAssembly.MainModule.GetType("Mirror.ClientRpcAttribute");
            TargetRpcType = mirrorAssembly.MainModule.GetType("Mirror.TargetRpcAttribute");
            SyncEventType = mirrorAssembly.MainModule.GetType("Mirror.SyncEventAttribute");
            SyncObjectType = mirrorAssembly.MainModule.GetType("Mirror.SyncObject");
        }

        static ModuleDefinition ResolveSystemModule(AssemblyDefinition currentAssembly)
        {
            AssemblyNameReference name = AssemblyNameReference.Parse("mscorlib");
            ReaderParameters parameters = new ReaderParameters
            {
                AssemblyResolver = currentAssembly.MainModule.AssemblyResolver
            };
            return currentAssembly.MainModule.AssemblyResolver.Resolve(name, parameters).MainModule;
        }

        static TypeReference ImportSystemModuleType(AssemblyDefinition currentAssembly, ModuleDefinition systemModule, string fullName)
        {
            TypeDefinition type = systemModule.GetType(fullName) ?? systemModule.ExportedTypes.First(t => t.FullName == fullName).Resolve();
            if (type != null)
            {
                return currentAssembly.MainModule.ImportReference(type);
            }
            Weaver.Error("Failed to import mscorlib type: " + fullName + " because Resolve failed. (Might happen when trying to Resolve in NetStandard dll, see also: https://github.com/vis2k/Mirror/issues/791)");
            return null;
        }

        public static void SetupTargetTypes(AssemblyDefinition unityAssembly, AssemblyDefinition mirrorAssembly, AssemblyDefinition currentAssembly)
        {
            // system types
            ModuleDefinition systemModule = ResolveSystemModule(currentAssembly);
            voidType = ImportSystemModuleType(currentAssembly, systemModule, "System.Void");
            singleType = ImportSystemModuleType(currentAssembly, systemModule, "System.Single");
            doubleType = ImportSystemModuleType(currentAssembly, systemModule, "System.Double");
            boolType = ImportSystemModuleType(currentAssembly, systemModule, "System.Boolean");
            int64Type = ImportSystemModuleType(currentAssembly, systemModule, "System.Int64");
            uint64Type = ImportSystemModuleType(currentAssembly, systemModule, "System.UInt64");
            int32Type = ImportSystemModuleType(currentAssembly, systemModule, "System.Int32");
            uint32Type = ImportSystemModuleType(currentAssembly, systemModule, "System.UInt32");
            objectType = ImportSystemModuleType(currentAssembly, systemModule, "System.Object");
            typeType = ImportSystemModuleType(currentAssembly, systemModule, "System.Type");
            IEnumeratorType = ImportSystemModuleType(currentAssembly, systemModule, "System.Collections.IEnumerator");

            ArraySegmentType = ImportSystemModuleType(currentAssembly, systemModule, "System.ArraySegment`1");
            ArraySegmentArrayReference = Resolvers.ResolveProperty(ArraySegmentType, currentAssembly, "Array");
            ArraySegmentCountReference = Resolvers.ResolveProperty(ArraySegmentType, currentAssembly, "Count");
            ArraySegmentOffsetReference = Resolvers.ResolveProperty(ArraySegmentType, currentAssembly, "Offset");
            ArraySegmentConstructorReference = Resolvers.ResolveMethod(ArraySegmentType, currentAssembly, ".ctor");

            NetworkReaderType = mirrorAssembly.MainModule.GetType("Mirror.NetworkReader");
            NetworkWriterType = mirrorAssembly.MainModule.GetType("Mirror.NetworkWriter");
            TypeReference pooledNetworkWriterTmp = mirrorAssembly.MainModule.GetType("Mirror.PooledNetworkWriter");
            PooledNetworkWriterType = currentAssembly.MainModule.ImportReference(pooledNetworkWriterTmp);

            NetworkServerGetActive = Resolvers.ResolveMethod(NetworkServerType, currentAssembly, "get_active");
            NetworkServerGetLocalClientActive = Resolvers.ResolveMethod(NetworkServerType, currentAssembly, "get_localClientActive");
            NetworkClientGetActive = Resolvers.ResolveMethod(NetworkClientType, currentAssembly, "get_active");

            CmdDelegateReference = mirrorAssembly.MainModule.GetType("Mirror.RemoteCalls.CmdDelegate");
            CmdDelegateConstructor = Resolvers.ResolveMethod(CmdDelegateReference, currentAssembly, ".ctor");

            currentAssembly.MainModule.ImportReference(gameObjectType);
            currentAssembly.MainModule.ImportReference(transformType);

            TypeReference networkIdentityTmp = mirrorAssembly.MainModule.GetType("Mirror.NetworkIdentity");
            NetworkIdentityType = currentAssembly.MainModule.ImportReference(networkIdentityTmp);

            NetworkBehaviourType = mirrorAssembly.MainModule.GetType("Mirror.NetworkBehaviour");
            RemoteCallHelperType = mirrorAssembly.MainModule.GetType("Mirror.RemoteCalls.RemoteCallHelper");
            NetworkConnectionType = mirrorAssembly.MainModule.GetType("Mirror.NetworkConnection");

            MonoBehaviourType = unityAssembly.MainModule.GetType("UnityEngine.MonoBehaviour");
            ScriptableObjectType = unityAssembly.MainModule.GetType("UnityEngine.ScriptableObject");

            ScriptableObjectCreateInstanceMethod = Resolvers.ResolveMethod(
                ScriptableObjectType, currentAssembly,
                md => md.Name == "CreateInstance" && md.HasGenericParameters);

            NetworkConnectionType = mirrorAssembly.MainModule.GetType("Mirror.NetworkConnection");
            NetworkConnectionType = currentAssembly.MainModule.ImportReference(NetworkConnectionType);

            MessageBaseType = mirrorAssembly.MainModule.GetType("Mirror.MessageBase");
            IMessageBaseType = mirrorAssembly.MainModule.GetType("Mirror.IMessageBase");
            SyncListType = mirrorAssembly.MainModule.GetType("Mirror.SyncList`1");
            SyncSetType = mirrorAssembly.MainModule.GetType("Mirror.SyncSet`1");
            SyncDictionaryType = mirrorAssembly.MainModule.GetType("Mirror.SyncDictionary`2");

            NetworkBehaviourDirtyBitsReference = Resolvers.ResolveProperty(NetworkBehaviourType, currentAssembly, "syncVarDirtyBits");
            TypeDefinition NetworkWriterPoolType = mirrorAssembly.MainModule.GetType("Mirror.NetworkWriterPool");
            GetPooledWriterReference = Resolvers.ResolveMethod(NetworkWriterPoolType, currentAssembly, "GetWriter");
            RecycleWriterReference = Resolvers.ResolveMethod(NetworkWriterPoolType, currentAssembly, "Recycle");

            ComponentType = unityAssembly.MainModule.GetType("UnityEngine.Component");
            ObjectType = unityAssembly.MainModule.GetType("UnityEngine.Object");
            ClientSceneType = mirrorAssembly.MainModule.GetType("Mirror.ClientScene");
            ReadyConnectionReference = Resolvers.ResolveMethod(ClientSceneType, currentAssembly, "get_readyConnection");

            syncVarEqualReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SyncVarEqual");
            syncVarNetworkIdentityEqualReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SyncVarNetworkIdentityEqual");
            syncVarGameObjectEqualReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SyncVarGameObjectEqual");
            setSyncVarReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SetSyncVar");
            setSyncVarHookGuard = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "setSyncVarHookGuard");
            getSyncVarHookGuard = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "getSyncVarHookGuard");

            setSyncVarGameObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SetSyncVarGameObject");
            getSyncVarGameObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "GetSyncVarGameObject");
            setSyncVarNetworkIdentityReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SetSyncVarNetworkIdentity");
            getSyncVarNetworkIdentityReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "GetSyncVarNetworkIdentity");
            registerCommandDelegateReference = Resolvers.ResolveMethod(RemoteCallHelperType, currentAssembly, "RegisterCommandDelegate");
            registerRpcDelegateReference = Resolvers.ResolveMethod(RemoteCallHelperType, currentAssembly, "RegisterRpcDelegate");
            registerEventDelegateReference = Resolvers.ResolveMethod(RemoteCallHelperType, currentAssembly, "RegisterEventDelegate");
            getTypeReference = Resolvers.ResolveMethod(objectType, currentAssembly, "GetType");
            getTypeFromHandleReference = Resolvers.ResolveMethod(typeType, currentAssembly, "GetTypeFromHandle");
            logErrorReference = Resolvers.ResolveMethod(unityAssembly.MainModule.GetType("UnityEngine.Debug"), currentAssembly, "LogError");
            logWarningReference = Resolvers.ResolveMethod(unityAssembly.MainModule.GetType("UnityEngine.Debug"), currentAssembly, "LogWarning");
            sendCommandInternal = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SendCommandInternal");
            sendRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SendRPCInternal");
            sendTargetRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SendTargetRPCInternal");
            sendEventInternal = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SendEventInternal");

            SyncObjectType = currentAssembly.MainModule.ImportReference(SyncObjectType);
            InitSyncObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "InitSyncObject");
        }
    }
}
