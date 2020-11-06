using System;
using Mono.Cecil;
using UnityEngine;

namespace Mirror.Weaver
{
    public static class WeaverTypes
    {
        public static MethodReference ScriptableObjectCreateInstanceMethod;

        public static MethodReference NetworkBehaviourDirtyBitsReference;

        public static MethodReference BehaviorConnectionToServerReference;

        public static MethodReference NetworkServerGetActive;
        public static MethodReference NetworkServerGetLocalClientActive;

        public static MethodReference NetworkBehaviourGetIdentity;

        public static MethodReference NetworkBehaviourIsServer;
        public static MethodReference NetworkBehaviourIsClient;
        public static MethodReference NetworkBehaviourIsLocalClient;
        public static MethodReference NetworkBehaviourHasAuthority;
        public static MethodReference NetworkBehaviourIsLocalPlayer;

        public static MethodReference MethodInvocationExceptionConstructor;

        // custom attribute types
        public static MethodReference syncVarEqualReference;
        public static MethodReference syncVarNetworkIdentityEqualReference;
        public static MethodReference setSyncVarHookGuard;
        public static MethodReference getSyncVarHookGuard;
        public static MethodReference setSyncVarNetworkIdentityReference;
        public static MethodReference getSyncVarNetworkIdentityReference;

        private static AssemblyDefinition currentAssembly;

        public static TypeReference Import<T>() => Import(typeof(T));

        public static TypeReference Import(Type t) => currentAssembly.MainModule.ImportReference(t);

        public static void SetupTargetTypes(AssemblyDefinition currentAssembly)
        {
            // system types
            WeaverTypes.currentAssembly = currentAssembly;

            TypeReference NetworkServerType = Import<NetworkServer>();
            NetworkServerGetActive = Resolvers.ResolveMethod(NetworkServerType, currentAssembly, "get_Active");
            NetworkServerGetLocalClientActive = Resolvers.ResolveMethod(NetworkServerType, currentAssembly, "get_LocalClientActive");

            TypeReference NetworkBehaviourType = Import<NetworkBehaviour>();
            TypeReference RemoteCallHelperType = Import(typeof(RemoteCalls.RemoteCallHelper));

            NetworkBehaviourGetIdentity = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "get_NetIdentity");

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

            BehaviorConnectionToServerReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "get_ConnectionToServer");

            syncVarEqualReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SyncVarEqual");
            syncVarNetworkIdentityEqualReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SyncVarNetworkIdentityEqual");
            setSyncVarHookGuard = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SetSyncVarHookGuard");
            getSyncVarHookGuard = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "GetSyncVarHookGuard");

            setSyncVarNetworkIdentityReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "SetSyncVarNetworkIdentity");
            getSyncVarNetworkIdentityReference = Resolvers.ResolveMethod(NetworkBehaviourType, currentAssembly, "GetSyncVarNetworkIdentity");
        }
    }
}
