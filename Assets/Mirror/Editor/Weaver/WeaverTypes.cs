using System;
using Mono.Cecil;
using UnityEngine;

namespace Mirror.Weaver
{
    public static class WeaverTypes
    {
        public static MethodReference ScriptableObjectCreateInstanceMethod;

        public static MethodReference NetworkBehaviourDirtyBitsReference;
        public static MethodReference GetPooledWriterReference;
        public static MethodReference RecycleWriterReference;

        public static MethodReference BehaviorConnectionToServerReference;

        public static MethodReference CmdDelegateConstructor;

        public static MethodReference NetworkServerGetActive;
        public static MethodReference NetworkServerGetLocalClientActive;
        public static MethodReference NetworkClientGetActive;

        public static MethodReference NetworkBehaviourGetIdentity;
        public static MethodReference NetworkIdentityGetServer;
        public static MethodReference NetworkIdentityGetClient;

        public static MethodReference NetworkBehaviourIsServer;
        public static MethodReference NetworkBehaviourIsClient;
        public static MethodReference NetworkBehaviourIsLocalClient;
        public static MethodReference NetworkBehaviourHasAuthority;
        public static MethodReference NetworkBehaviourIsLocalPlayer;

        public static MethodReference MethodInvocationExceptionConstructor;

        // custom attribute types
        public static MethodReference InitSyncObjectReference;

        // array segment
        public static MethodReference ArraySegmentConstructorReference;
        public static MethodReference ArraySegmentArrayReference;
        public static MethodReference ArraySegmentOffsetReference;
        public static MethodReference ArraySegmentCountReference;

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
        public static MethodReference registerServerRpcDelegateReference;
        public static MethodReference registerRpcDelegateReference;
        public static MethodReference getTypeReference;
        public static MethodReference getTypeFromHandleReference;
        public static MethodReference logErrorReference;
        public static MethodReference logWarningReference;
        public static MethodReference sendServerRpcInternal;
        public static MethodReference sendRpcInternal;
        public static MethodReference sendTargetRpcInternal;

        private static AssemblyDefinition currentAssembly;

        public static TypeReference Import<T>() => Import(typeof(T));

        public static TypeReference Import(Type t) => currentAssembly.MainModule.ImportReference(t);

        public static void SetupTargetTypes(AssemblyDefinition currentAssembly)
        {
            // system types
            WeaverTypes.currentAssembly = currentAssembly;

            TypeReference ArraySegmentType = Import(typeof(ArraySegment<>));
            ArraySegmentArrayReference = Resolvers.ResolveProperty(ArraySegmentType, currentAssembly, "Array");
            ArraySegmentCountReference = Resolvers.ResolveProperty(ArraySegmentType, currentAssembly, "Count");
            ArraySegmentOffsetReference = Resolvers.ResolveProperty(ArraySegmentType, currentAssembly, "Offset");
            ArraySegmentConstructorReference = Resolvers.ResolveMethod(ArraySegmentType, currentAssembly, ".ctor");

            TypeReference ListType = Import(typeof(System.Collections.Generic.List<>));
            ListCountReference = Resolvers.ResolveProperty(ListType, currentAssembly, "Count");
            ListGetItemReference = Resolvers.ResolveMethod(ListType, currentAssembly, "get_Item");
            ListAddReference = Resolvers.ResolveMethod(ListType, currentAssembly, "Add");
            ListConstructorReference = Resolvers.ResolveMethod(ListType, currentAssembly, ".ctor");

            TypeReference NetworkServerType = Import<NetworkServer>();
            NetworkServerGetActive = Resolvers.ResolveMethod(NetworkServerType, currentAssembly, "get_Active");
            NetworkServerGetLocalClientActive = Resolvers.ResolveMethod(NetworkServerType, currentAssembly, "get_LocalClientActive");
            TypeReference NetworkClientType = Import<NetworkClient>();
            NetworkClientGetActive = Resolvers.ResolveMethod(NetworkClientType, currentAssembly, "get_Active");

            TypeReference cmdDelegateReference = Import<RemoteCalls.CmdDelegate>();
            CmdDelegateConstructor = Resolvers.ResolveMethod(cmdDelegateReference, currentAssembly, ".ctor");

            TypeReference NetworkBehaviourType = Import<NetworkBehaviour>();
            TypeReference RemoteCallHelperType = Import(typeof(RemoteCalls.RemoteCallHelper));

            NetworkBehaviourGetIdentity = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "get_NetIdentity");
            TypeReference NetworkIdentityType = Import<NetworkIdentity>();
            NetworkIdentityGetServer = Resolvers.ResolveMethod(NetworkIdentityType, currentAssembly, "get_Server");
            NetworkIdentityGetClient = Resolvers.ResolveMethod(NetworkIdentityType, currentAssembly, "get_Client");

            NetworkBehaviourIsServer = Resolvers.ResolveProperty(NetworkBehaviourType, currentAssembly, "IsServer");
            NetworkBehaviourIsClient = Resolvers.ResolveProperty(NetworkBehaviourType, currentAssembly, "IsClient");
            NetworkBehaviourIsLocalClient = Resolvers.ResolveProperty(NetworkBehaviourType, currentAssembly, "IsLocalClient");
            NetworkBehaviourHasAuthority = Resolvers.ResolveProperty(NetworkBehaviourType, currentAssembly, "HasAuthority");
            NetworkBehaviourIsLocalPlayer = Resolvers.ResolveProperty(NetworkBehaviourType, currentAssembly, "IsLocalPlayer");

            TypeReference ScriptableObjectType = Import<ScriptableObject>();
            ScriptableObjectCreateInstanceMethod = Resolvers.ResolveMethod(
                ScriptableObjectType, currentAssembly,
                md => md.Name == "CreateInstance" && md.HasGenericParameters);

            TypeReference MethodInvocationExceptionType = Import<MethodInvocationException>();
            MethodInvocationExceptionConstructor = Resolvers.ResolveMethodWithArg(MethodInvocationExceptionType, currentAssembly, ".ctor", "System.String");

            NetworkBehaviourDirtyBitsReference = Resolvers.ResolveProperty(NetworkBehaviourType, currentAssembly, "SyncVarDirtyBits");
            TypeReference NetworkWriterPoolType = Import(typeof(NetworkWriterPool));
            GetPooledWriterReference = Resolvers.ResolveMethod(NetworkWriterPoolType, currentAssembly, "GetWriter");
            RecycleWriterReference = Resolvers.ResolveMethod(NetworkWriterPoolType, currentAssembly, "Recycle");

            BehaviorConnectionToServerReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "get_ConnectionToServer");

            syncVarEqualReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SyncVarEqual");
            syncVarNetworkIdentityEqualReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SyncVarNetworkIdentityEqual");
            syncVarGameObjectEqualReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SyncVarGameObjectEqual");
            setSyncVarReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SetSyncVar");
            setSyncVarHookGuard = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SetSyncVarHookGuard");
            getSyncVarHookGuard = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "GetSyncVarHookGuard");

            setSyncVarGameObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SetSyncVarGameObject");
            getSyncVarGameObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "GetSyncVarGameObject");
            setSyncVarNetworkIdentityReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SetSyncVarNetworkIdentity");
            getSyncVarNetworkIdentityReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "GetSyncVarNetworkIdentity");
            registerServerRpcDelegateReference = Resolvers.ResolveMethod(RemoteCallHelperType, currentAssembly, "RegisterServerRpcDelegate");
            registerRpcDelegateReference = Resolvers.ResolveMethod(RemoteCallHelperType, currentAssembly, "RegisterRpcDelegate");
            TypeReference unityDebug = Import(typeof(Debug));
            logErrorReference = Resolvers.ResolveMethod(unityDebug, currentAssembly, "LogError");
            logWarningReference = Resolvers.ResolveMethod(unityDebug, currentAssembly, "LogWarning");

            TypeReference typeType = Import(typeof(Type));
            getTypeFromHandleReference = Resolvers.ResolveMethod(typeType, currentAssembly, "GetTypeFromHandle");
            sendServerRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SendServerRpcInternal");
            sendRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SendRPCInternal");
            sendTargetRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SendTargetRPCInternal");

            InitSyncObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "InitSyncObject");

        }
    }
}
