using System;
using System.Reflection;
using Mono.CecilX;

namespace Mirror.Weaver
{
    public static class WeaverTypes
    {
        // Network types
        public static TypeReference RemoteCallHelperType;
        public static TypeReference ScriptableObjectType;

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
        public static MethodReference registerCommandDelegateReference;
        public static MethodReference registerRpcDelegateReference;
        public static MethodReference getTypeReference;
        public static MethodReference logErrorReference;
        public static MethodReference logWarningReference;
        public static MethodReference sendCommandInternal;
        public static MethodReference sendRpcInternal;
        public static MethodReference sendTargetRpcInternal;

        private static AssemblyDefinition currentAssembly;

        public static TypeReference Import<T>() => Import(typeof(T));

        public static TypeReference Import(Type t) => currentAssembly.MainModule.ImportReference(t);

        public static MethodReference Import(MethodInfo method) => currentAssembly.MainModule.ImportReference(method);

        public static MethodReference Import<A>(Func<A> del) => Import(del.GetMethodInfo());
        public static MethodReference Import<A, B>(Func<A, B> del) => Import(del.GetMethodInfo());
        public static MethodReference Import<A, B, C>(Func<A, B, C> del) => Import(del.GetMethodInfo());

        public static void SetupTargetTypes(AssemblyDefinition unityAssembly, AssemblyDefinition mirrorAssembly, AssemblyDefinition currentAssembly)
        {
            // system types
            WeaverTypes.currentAssembly = currentAssembly;

            TypeReference ArraySegmentType = Import(typeof(System.ArraySegment<>));
            ArraySegmentArrayReference = Resolvers.ResolveProperty(ArraySegmentType, currentAssembly, "Array");
            ArraySegmentCountReference = Resolvers.ResolveProperty(ArraySegmentType, currentAssembly, "Count");
            ArraySegmentOffsetReference = Resolvers.ResolveProperty(ArraySegmentType, currentAssembly, "Offset");
            ArraySegmentConstructorReference = Resolvers.ResolveMethod(ArraySegmentType, currentAssembly, ".ctor");

            TypeReference ListType = Import(typeof(System.Collections.Generic.List<>));
            ListCountReference = Resolvers.ResolveProperty(ListType, currentAssembly, "Count");
            ListGetItemReference = Resolvers.ResolveMethod(ListType, currentAssembly, "get_Item");
            ListAddReference = Resolvers.ResolveMethod(ListType, currentAssembly, "Add");
            ListConstructorReference = Resolvers.ResolveMethod(ListType, currentAssembly, ".ctor");

            TypeDefinition NetworkServerType = mirrorAssembly.MainModule.GetType("Mirror.NetworkServer");
            NetworkServerGetActive = Resolvers.ResolveMethod(NetworkServerType, currentAssembly, "get_active");
            NetworkServerGetLocalClientActive = Resolvers.ResolveMethod(NetworkServerType, currentAssembly, "get_localClientActive");
            TypeDefinition NetworkClientType = mirrorAssembly.MainModule.GetType("Mirror.NetworkClient");
            NetworkClientGetActive = Resolvers.ResolveMethod(NetworkClientType, currentAssembly, "get_active");

            TypeDefinition cmdDelegateReference = mirrorAssembly.MainModule.GetType("Mirror.RemoteCalls.CmdDelegate");
            CmdDelegateConstructor = Resolvers.ResolveMethod(cmdDelegateReference, currentAssembly, ".ctor");

            TypeReference NetworkBehaviourType = Import<Mirror.NetworkBehaviour>();
            RemoteCallHelperType = mirrorAssembly.MainModule.GetType("Mirror.RemoteCalls.RemoteCallHelper");

            ScriptableObjectType = unityAssembly.MainModule.GetType("UnityEngine.ScriptableObject");

            ScriptableObjectCreateInstanceMethod = Resolvers.ResolveMethod(
                ScriptableObjectType, currentAssembly,
                md => md.Name == "CreateInstance" && md.HasGenericParameters);

            NetworkBehaviourDirtyBitsReference = Resolvers.ResolveProperty(NetworkBehaviourType, currentAssembly, "syncVarDirtyBits");
            TypeDefinition NetworkWriterPoolType = mirrorAssembly.MainModule.GetType("Mirror.NetworkWriterPool");
            GetPooledWriterReference = Resolvers.ResolveMethod(NetworkWriterPoolType, currentAssembly, "GetWriter");
            RecycleWriterReference = Resolvers.ResolveMethod(NetworkWriterPoolType, currentAssembly, "Recycle");

            TypeDefinition ClientSceneType = mirrorAssembly.MainModule.GetType("Mirror.ClientScene");
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
            logErrorReference = Resolvers.ResolveMethod(unityAssembly.MainModule.GetType("UnityEngine.Debug"), currentAssembly, "LogError");
            logWarningReference = Resolvers.ResolveMethod(unityAssembly.MainModule.GetType("UnityEngine.Debug"), currentAssembly, "LogWarning");
            sendCommandInternal = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SendCommandInternal");
            sendRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SendRPCInternal");
            sendTargetRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SendTargetRPCInternal");

            InitSyncObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "InitSyncObject");
        }
    }
}
