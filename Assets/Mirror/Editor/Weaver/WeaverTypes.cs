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

        AssemblyDefinition assembly;

        public TypeReference Import<T>() => Import(typeof(T));

        public TypeReference Import(Type t) => assembly.MainModule.ImportReference(t);

        // constructor resolves the types and stores them in fields
        public WeaverTypes(AssemblyDefinition assembly)
        {
            // system types
            this.assembly = assembly;

            TypeReference ArraySegmentType = Import(typeof(ArraySegment<>));
            ArraySegmentConstructorReference = Resolvers.ResolveMethod(ArraySegmentType, assembly, ".ctor");

            TypeReference ListType = Import(typeof(System.Collections.Generic.List<>));

            TypeReference NetworkServerType = Import(typeof(NetworkServer));
            NetworkServerGetActive = Resolvers.ResolveMethod(NetworkServerType, assembly, "get_active");
            NetworkServerGetLocalClientActive = Resolvers.ResolveMethod(NetworkServerType, assembly, "get_localClientActive");
            TypeReference NetworkClientType = Import(typeof(NetworkClient));
            NetworkClientGetActive = Resolvers.ResolveMethod(NetworkClientType, assembly, "get_active");

            TypeReference cmdDelegateReference = Import<RemoteCalls.CmdDelegate>();
            CmdDelegateConstructor = Resolvers.ResolveMethod(cmdDelegateReference, assembly, ".ctor");

            TypeReference NetworkBehaviourType = Import<NetworkBehaviour>();
            TypeReference RemoteCallHelperType = Import(typeof(RemoteCalls.RemoteCallHelper));

            TypeReference ScriptableObjectType = Import<UnityEngine.ScriptableObject>();

            ScriptableObjectCreateInstanceMethod = Resolvers.ResolveMethod(
                ScriptableObjectType, assembly,
                md => md.Name == "CreateInstance" && md.HasGenericParameters);

            NetworkBehaviourDirtyBitsReference = Resolvers.ResolveProperty(NetworkBehaviourType, assembly, "syncVarDirtyBits");
            TypeReference NetworkWriterPoolType = Import(typeof(NetworkWriterPool));
            GetPooledWriterReference = Resolvers.ResolveMethod(NetworkWriterPoolType, assembly, "GetWriter");
            RecycleWriterReference = Resolvers.ResolveMethod(NetworkWriterPoolType, assembly, "Recycle");

            ReadyConnectionReference = Resolvers.ResolveMethod(NetworkClientType, assembly, "get_readyConnection");

            syncVarEqualReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, "SyncVarEqual");
            syncVarNetworkIdentityEqualReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, "SyncVarNetworkIdentityEqual");
            syncVarGameObjectEqualReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, "SyncVarGameObjectEqual");
            setSyncVarReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, "SetSyncVar");
            setSyncVarHookGuard = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, "setSyncVarHookGuard");
            getSyncVarHookGuard = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, "getSyncVarHookGuard");

            setSyncVarGameObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, "SetSyncVarGameObject");
            getSyncVarGameObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, "GetSyncVarGameObject");
            setSyncVarNetworkIdentityReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, "SetSyncVarNetworkIdentity");
            getSyncVarNetworkIdentityReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, "GetSyncVarNetworkIdentity");
            syncVarNetworkBehaviourEqualReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, "SyncVarNetworkBehaviourEqual");
            setSyncVarNetworkBehaviourReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, "SetSyncVarNetworkBehaviour");
            getSyncVarNetworkBehaviourReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, "GetSyncVarNetworkBehaviour");

            registerCommandDelegateReference = Resolvers.ResolveMethod(RemoteCallHelperType, assembly, "RegisterCommandDelegate");
            registerRpcDelegateReference = Resolvers.ResolveMethod(RemoteCallHelperType, assembly, "RegisterRpcDelegate");

            TypeReference unityDebug = Import(typeof(UnityEngine.Debug));
            // these have multiple methods with same name, so need to check parameters too
            logErrorReference = Resolvers.ResolveMethod(unityDebug, assembly, md =>
                md.Name == "LogError" &&
                md.Parameters.Count == 1 &&
                md.Parameters[0].ParameterType.FullName == typeof(object).FullName);

            logWarningReference = Resolvers.ResolveMethod(unityDebug, assembly, md =>
                md.Name == "LogWarning" &&
                md.Parameters.Count == 1 &&
                md.Parameters[0].ParameterType.FullName == typeof(object).FullName);

            TypeReference typeType = Import(typeof(Type));
            getTypeFromHandleReference = Resolvers.ResolveMethod(typeType, assembly, "GetTypeFromHandle");
            sendCommandInternal = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, "SendCommandInternal");
            sendRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, "SendRPCInternal");
            sendTargetRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, "SendTargetRPCInternal");

            InitSyncObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, "InitSyncObject");

            TypeReference readerExtensions = Import(typeof(NetworkReaderExtensions));
            readNetworkBehaviourGeneric = Resolvers.ResolveMethod(readerExtensions, assembly, (md =>
            {
                return md.Name == nameof(NetworkReaderExtensions.ReadNetworkBehaviour) &&
                    md.HasGenericParameters;
            }));
        }
    }
}
