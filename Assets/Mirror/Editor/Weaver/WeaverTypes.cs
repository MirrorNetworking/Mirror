using System;
using Mono.CecilX;

namespace Mirror.Weaver
{
    // not static, because ILPostProcessor is multithreaded
    public class WeaverTypes
    {
        public MethodReference ScriptableObjectCreateInstanceMethod;

        public MethodReference NetworkBehaviourDirtyBitsReference;
        public MethodReference GetPooledWriterReference;
        public MethodReference RecycleWriterReference;

        public MethodReference ReadyConnectionReference;

        public MethodReference CmdDelegateConstructor;

        public MethodReference NetworkServerGetActive;
        public MethodReference NetworkServerGetLocalClientActive;
        public MethodReference NetworkClientGetActive;

        // custom attribute types
        public MethodReference InitSyncObjectReference;

        // array segment
        public MethodReference ArraySegmentConstructorReference;

        // syncvar
        public MethodReference syncVarEqualReference;
        public MethodReference syncVarNetworkIdentityEqualReference;
        public MethodReference syncVarGameObjectEqualReference;
        public MethodReference setSyncVarReference;
        public MethodReference setSyncVarHookGuard;
        public MethodReference getSyncVarHookGuard;
        public MethodReference setSyncVarGameObjectReference;
        public MethodReference getSyncVarGameObjectReference;
        public MethodReference setSyncVarNetworkIdentityReference;
        public MethodReference getSyncVarNetworkIdentityReference;
        public MethodReference syncVarNetworkBehaviourEqualReference;
        public MethodReference setSyncVarNetworkBehaviourReference;
        public MethodReference getSyncVarNetworkBehaviourReference;
        public MethodReference registerCommandDelegateReference;
        public MethodReference registerRpcDelegateReference;
        public MethodReference getTypeFromHandleReference;
        public MethodReference logErrorReference;
        public MethodReference logWarningReference;
        public MethodReference sendCommandInternal;
        public MethodReference sendRpcInternal;
        public MethodReference sendTargetRpcInternal;

        public MethodReference readNetworkBehaviourGeneric;

        AssemblyDefinition currentAssembly;

        public TypeReference Import<T>() => Import(typeof(T));

        public TypeReference Import(Type t) => currentAssembly.MainModule.ImportReference(t);

        // constructor resolves the types and stores them in fields
        public WeaverTypes(AssemblyDefinition currentAssembly)
        {
            // system types
            this.currentAssembly = currentAssembly;

            TypeReference ArraySegmentType = Import(typeof(ArraySegment<>));
            ArraySegmentConstructorReference = Resolvers.ResolveMethod(ArraySegmentType, currentAssembly, ".ctor");

            TypeReference ListType = Import(typeof(System.Collections.Generic.List<>));

            TypeReference NetworkServerType = Import(typeof(NetworkServer));
            NetworkServerGetActive = Resolvers.ResolveMethod(NetworkServerType, currentAssembly, "get_active");
            NetworkServerGetLocalClientActive = Resolvers.ResolveMethod(NetworkServerType, currentAssembly, "get_localClientActive");
            TypeReference NetworkClientType = Import(typeof(NetworkClient));
            NetworkClientGetActive = Resolvers.ResolveMethod(NetworkClientType, currentAssembly, "get_active");

            TypeReference cmdDelegateReference = Import<RemoteCalls.CmdDelegate>();
            CmdDelegateConstructor = Resolvers.ResolveMethod(cmdDelegateReference, currentAssembly, ".ctor");

            TypeReference NetworkBehaviourType = Import<NetworkBehaviour>();
            TypeReference RemoteCallHelperType = Import(typeof(RemoteCalls.RemoteCallHelper));

            TypeReference ScriptableObjectType = Import<UnityEngine.ScriptableObject>();

            ScriptableObjectCreateInstanceMethod = Resolvers.ResolveMethod(
                ScriptableObjectType, currentAssembly,
                md => md.Name == "CreateInstance" && md.HasGenericParameters);

            NetworkBehaviourDirtyBitsReference = Resolvers.ResolveProperty(NetworkBehaviourType, currentAssembly, "syncVarDirtyBits");
            TypeReference NetworkWriterPoolType = Import(typeof(NetworkWriterPool));
            GetPooledWriterReference = Resolvers.ResolveMethod(NetworkWriterPoolType, currentAssembly, "GetWriter");
            RecycleWriterReference = Resolvers.ResolveMethod(NetworkWriterPoolType, currentAssembly, "Recycle");

            ReadyConnectionReference = Resolvers.ResolveMethod(NetworkClientType, currentAssembly, "get_readyConnection");

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
            syncVarNetworkBehaviourEqualReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SyncVarNetworkBehaviourEqual");
            setSyncVarNetworkBehaviourReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SetSyncVarNetworkBehaviour");
            getSyncVarNetworkBehaviourReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "GetSyncVarNetworkBehaviour");

            registerCommandDelegateReference = Resolvers.ResolveMethod(RemoteCallHelperType, currentAssembly, "RegisterCommandDelegate");
            registerRpcDelegateReference = Resolvers.ResolveMethod(RemoteCallHelperType, currentAssembly, "RegisterRpcDelegate");

            TypeReference unityDebug = Import(typeof(UnityEngine.Debug));
            // these have multiple methods with same name, so need to check parameters too
            logErrorReference = Resolvers.ResolveMethod(unityDebug, currentAssembly, md =>
                md.Name == "LogError" &&
                md.Parameters.Count == 1 &&
                md.Parameters[0].ParameterType.FullName == typeof(object).FullName);

            logWarningReference = Resolvers.ResolveMethod(unityDebug, currentAssembly, md =>
                md.Name == "LogWarning" &&
                md.Parameters.Count == 1 &&
                md.Parameters[0].ParameterType.FullName == typeof(object).FullName);

            TypeReference typeType = Import(typeof(Type));
            getTypeFromHandleReference = Resolvers.ResolveMethod(typeType, currentAssembly, "GetTypeFromHandle");
            sendCommandInternal = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SendCommandInternal");
            sendRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SendRPCInternal");
            sendTargetRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SendTargetRPCInternal");

            InitSyncObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "InitSyncObject");

            TypeReference readerExtensions = Import(typeof(NetworkReaderExtensions));
            readNetworkBehaviourGeneric = Resolvers.ResolveMethod(readerExtensions, currentAssembly, (md =>
            {
                return md.Name == nameof(NetworkReaderExtensions.ReadNetworkBehaviour) &&
                    md.HasGenericParameters;
            }));
        }
    }
}
