using System;
using System.Reflection;
using Mono.CecilX;

namespace Mirror.Weaver
{
    public static class WeaverTypes
    {
        public static MethodReference ScriptableObjectCreateInstanceMethod;

        public static MethodReference NetworkBehaviourDirtyBitsReference;
        public static MethodReference GetPooledWriterReference;
        public static MethodReference RecycleWriterReference;

        public static MethodReference ReadyConnectionReference;

        public static MethodReference CmdDelegateConstructor;

        public static MethodReference NetworkServerGetActive;
        public static MethodReference NetworkServerGetLocalClientActive;
        public static MethodReference NetworkClientGetActive;

        // custom attribute types
        public static MethodReference InitSyncObjectReference;

        // list
        public static MethodReference ListConstructorReference;
        public static MethodReference ListCountReference;
        public static MethodReference ListGetItemReference;
        public static MethodReference ListAddReference;

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
        public static MethodReference getTypeReference;
        public static MethodReference getTypeFromHandleReference;
        public static MethodReference logErrorReference;
        public static MethodReference logWarningReference;
        public static MethodReference sendCommandInternal;
        public static MethodReference sendRpcInternal;
        public static MethodReference sendTargetRpcInternal;

        private static AssemblyDefinition currentAssembly;

        public static TypeReference Import<T>() => Import(typeof(T));

        public static TypeReference Import(Type t) => currentAssembly.MainModule.ImportReference(t);

        public static MethodReference Import(MethodBase method) => currentAssembly.MainModule.ImportReference(method);
        public static MethodReference Import(Type t, string method) => Import(t.GetMethod(method));
        public static MethodReference Import<A>(string method) => Import(typeof(A), method);
        public static MethodReference ImportGet(Type t, string property) => Import(t.GetProperty(property).GetGetMethod());
        public static MethodReference ImportConstructor(Type t, params Type[] args) => Import(t.GetConstructor(args));

        public static void SetupTargetTypes(AssemblyDefinition currentAssembly)
        {
            // system types
            WeaverTypes.currentAssembly = currentAssembly;

            TypeReference ListType = Import(typeof(System.Collections.Generic.List<>));
            ListCountReference = Resolvers.ResolveProperty(ListType, currentAssembly, "Count");
            ListGetItemReference = Resolvers.ResolveMethod(ListType, currentAssembly, "get_Item");
            ListAddReference = Resolvers.ResolveMethod(ListType, currentAssembly, "Add");
            ListConstructorReference = Resolvers.ResolveMethod(ListType, currentAssembly, ".ctor");

            TypeReference NetworkServerType = Import(typeof(NetworkServer));
            NetworkServerGetActive = Resolvers.ResolveMethod(NetworkServerType, currentAssembly, "get_active");
            NetworkServerGetLocalClientActive = Resolvers.ResolveMethod(NetworkServerType, currentAssembly, "get_localClientActive");
            TypeReference NetworkClientType = Import(typeof(NetworkClient));
            NetworkClientGetActive = Resolvers.ResolveMethod(NetworkClientType, currentAssembly, "get_active");

            TypeReference cmdDelegateReference = Import<RemoteCalls.CmdDelegate>();
            CmdDelegateConstructor = Resolvers.ResolveMethod(cmdDelegateReference, currentAssembly, ".ctor");

            TypeReference NetworkBehaviourType = Import<NetworkBehaviour>();

            TypeReference ScriptableObjectType = Import<UnityEngine.ScriptableObject>();

            ScriptableObjectCreateInstanceMethod = Resolvers.ResolveMethod(
                ScriptableObjectType, currentAssembly,
                md => md.Name == "CreateInstance" && md.HasGenericParameters);

            NetworkBehaviourDirtyBitsReference = Resolvers.ResolveProperty(NetworkBehaviourType, currentAssembly, "syncVarDirtyBits");
            TypeReference NetworkWriterPoolType = Import(typeof(NetworkWriterPool));
            GetPooledWriterReference = Resolvers.ResolveMethod(NetworkWriterPoolType, currentAssembly, "GetWriter");
            RecycleWriterReference = Resolvers.ResolveMethod(NetworkWriterPoolType, currentAssembly, "Recycle");

            TypeReference ClientSceneType = Import(typeof(ClientScene));
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
            TypeReference unityDebug = Import(typeof(UnityEngine.Debug));
            logErrorReference = Resolvers.ResolveMethod(unityDebug, currentAssembly, "LogError");
            logWarningReference = Resolvers.ResolveMethod(unityDebug, currentAssembly, "LogWarning");

            TypeReference typeType = Import(typeof(Type));
            getTypeFromHandleReference = Resolvers.ResolveMethod(typeType, currentAssembly, "GetTypeFromHandle");
            sendCommandInternal = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SendCommandInternal");
            sendRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SendRPCInternal");
            sendTargetRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SendTargetRPCInternal");

            InitSyncObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "InitSyncObject");
        }
    }
}
