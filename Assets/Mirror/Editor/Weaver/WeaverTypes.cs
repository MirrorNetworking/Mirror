using System;
using Mono.Cecil;
using UnityEngine;

namespace Mirror.Weaver
{
    public static class WeaverTypes
    {
        public static MethodReference ScriptableObjectCreateInstanceMethod;

        public static MethodReference NetworkBehaviourIsServer;
        public static MethodReference NetworkBehaviourIsClient;
        public static MethodReference NetworkBehaviourIsLocalClient;
        public static MethodReference NetworkBehaviourHasAuthority;
        public static MethodReference NetworkBehaviourIsLocalPlayer;

        // custom attribute types

        private static AssemblyDefinition currentAssembly;

        public static TypeReference Import<T>() => Import(typeof(T));

        public static TypeReference Import(Type t) => currentAssembly.MainModule.ImportReference(t);

        public static void SetupTargetTypes(AssemblyDefinition currentAssembly)
        {
            // system types
            WeaverTypes.currentAssembly = currentAssembly;

            TypeReference NetworkBehaviourType = Import<NetworkBehaviour>();

            NetworkBehaviourIsServer = Resolvers.ResolveProperty(NetworkBehaviourType, currentAssembly, "IsServer");
            NetworkBehaviourIsClient = Resolvers.ResolveProperty(NetworkBehaviourType, currentAssembly, "IsClient");
            NetworkBehaviourIsLocalClient = Resolvers.ResolveProperty(NetworkBehaviourType, currentAssembly, "IsLocalClient");
            NetworkBehaviourHasAuthority = Resolvers.ResolveProperty(NetworkBehaviourType, currentAssembly, "HasAuthority");
            NetworkBehaviourIsLocalPlayer = Resolvers.ResolveProperty(NetworkBehaviourType, currentAssembly, "IsLocalPlayer");

            TypeReference ScriptableObjectType = Import<ScriptableObject>();
            ScriptableObjectCreateInstanceMethod = Resolvers.ResolveMethod(
                ScriptableObjectType, currentAssembly,
                md => md.Name == "CreateInstance" && md.HasGenericParameters);
        }
    }
}
