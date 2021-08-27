using System;
using Mono.CecilX;
using UnityEditor;
using UnityEngine;

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

        // attributes
        public TypeDefinition initializeOnLoadMethodAttribute;
        public TypeDefinition runtimeInitializeOnLoadMethodAttribute;

        AssemblyDefinition assembly;

        public TypeReference Import<T>() => Import(typeof(T));

        public TypeReference Import(Type t) => assembly.MainModule.ImportReference(t);

        // constructor resolves the types and stores them in fields
        public WeaverTypes(AssemblyDefinition assembly, Logger Log, ref bool WeavingFailed)
        {
            // system types
            this.assembly = assembly;

            TypeReference ArraySegmentType = Import(typeof(ArraySegment<>));
            ArraySegmentConstructorReference = Resolvers.ResolveMethod(ArraySegmentType, assembly, Log, ".ctor", ref WeavingFailed);

            TypeReference NetworkServerType = Import(typeof(NetworkServer));
            NetworkServerGetActive = Resolvers.ResolveMethod(NetworkServerType, assembly, Log, "get_active", ref WeavingFailed);
            NetworkServerGetLocalClientActive = Resolvers.ResolveMethod(NetworkServerType, assembly, Log, "get_localClientActive", ref WeavingFailed);
            TypeReference NetworkClientType = Import(typeof(NetworkClient));
            NetworkClientGetActive = Resolvers.ResolveMethod(NetworkClientType, assembly, Log, "get_active", ref WeavingFailed);

            TypeReference cmdDelegateReference = Import<RemoteCalls.CmdDelegate>();
            CmdDelegateConstructor = Resolvers.ResolveMethod(cmdDelegateReference, assembly, Log, ".ctor", ref WeavingFailed);

            TypeReference NetworkBehaviourType = Import<NetworkBehaviour>();
            TypeReference RemoteCallHelperType = Import(typeof(RemoteCalls.RemoteCallHelper));

            TypeReference ScriptableObjectType = Import<UnityEngine.ScriptableObject>();

            ScriptableObjectCreateInstanceMethod = Resolvers.ResolveMethod(
                ScriptableObjectType, assembly, Log,
                md => md.Name == "CreateInstance" && md.HasGenericParameters,
                ref WeavingFailed);

            NetworkBehaviourDirtyBitsReference = Resolvers.ResolveProperty(NetworkBehaviourType, assembly, "syncVarDirtyBits");
            TypeReference NetworkWriterPoolType = Import(typeof(NetworkWriterPool));
            GetPooledWriterReference = Resolvers.ResolveMethod(NetworkWriterPoolType, assembly, Log, "GetWriter", ref WeavingFailed);
            RecycleWriterReference = Resolvers.ResolveMethod(NetworkWriterPoolType, assembly, Log, "Recycle", ref WeavingFailed);

            ReadyConnectionReference = Resolvers.ResolveMethod(NetworkClientType, assembly, Log, "get_readyConnection", ref WeavingFailed);

            syncVarEqualReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "SyncVarEqual", ref WeavingFailed);
            syncVarNetworkIdentityEqualReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "SyncVarNetworkIdentityEqual", ref WeavingFailed);
            syncVarGameObjectEqualReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "SyncVarGameObjectEqual", ref WeavingFailed);
            setSyncVarReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "SetSyncVar", ref WeavingFailed);
            setSyncVarHookGuard = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "setSyncVarHookGuard", ref WeavingFailed);
            getSyncVarHookGuard = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "getSyncVarHookGuard", ref WeavingFailed);

            setSyncVarGameObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "SetSyncVarGameObject", ref WeavingFailed);
            getSyncVarGameObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "GetSyncVarGameObject", ref WeavingFailed);
            setSyncVarNetworkIdentityReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "SetSyncVarNetworkIdentity", ref WeavingFailed);
            getSyncVarNetworkIdentityReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "GetSyncVarNetworkIdentity", ref WeavingFailed);
            syncVarNetworkBehaviourEqualReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "SyncVarNetworkBehaviourEqual", ref WeavingFailed);
            setSyncVarNetworkBehaviourReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "SetSyncVarNetworkBehaviour", ref WeavingFailed);
            getSyncVarNetworkBehaviourReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "GetSyncVarNetworkBehaviour", ref WeavingFailed);

            registerCommandDelegateReference = Resolvers.ResolveMethod(RemoteCallHelperType, assembly, Log, "RegisterCommandDelegate", ref WeavingFailed);
            registerRpcDelegateReference = Resolvers.ResolveMethod(RemoteCallHelperType, assembly, Log, "RegisterRpcDelegate", ref WeavingFailed);

            TypeReference unityDebug = Import(typeof(UnityEngine.Debug));
            // these have multiple methods with same name, so need to check parameters too
            logErrorReference = Resolvers.ResolveMethod(unityDebug, assembly, Log, md =>
                md.Name == "LogError" &&
                md.Parameters.Count == 1 &&
                md.Parameters[0].ParameterType.FullName == typeof(object).FullName,
                ref WeavingFailed);

            logWarningReference = Resolvers.ResolveMethod(unityDebug, assembly, Log, md =>
                md.Name == "LogWarning" &&
                md.Parameters.Count == 1 &&
                md.Parameters[0].ParameterType.FullName == typeof(object).FullName,
                ref WeavingFailed);

            TypeReference typeType = Import(typeof(Type));
            getTypeFromHandleReference = Resolvers.ResolveMethod(typeType, assembly, Log, "GetTypeFromHandle", ref WeavingFailed);
            sendCommandInternal = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "SendCommandInternal", ref WeavingFailed);
            sendRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "SendRPCInternal", ref WeavingFailed);
            sendTargetRpcInternal = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "SendTargetRPCInternal", ref WeavingFailed);

            InitSyncObjectReference = Resolvers.ResolveMethod(NetworkBehaviourType, assembly, Log, "InitSyncObject", ref WeavingFailed);

            TypeReference readerExtensions = Import(typeof(NetworkReaderExtensions));
            readNetworkBehaviourGeneric = Resolvers.ResolveMethod(readerExtensions, assembly, Log, (md =>
            {
                return md.Name == nameof(NetworkReaderExtensions.ReadNetworkBehaviour) &&
                       md.HasGenericParameters;
            }),
            ref WeavingFailed);

            // [InitializeOnLoadMethod]
            // 'UnityEditor' is not available in builds.
            // we can only import this attribute if we are in an Editor assembly.
            if (Helpers.IsEditorAssembly(assembly))
            {
                TypeReference initializeOnLoadMethodAttributeRef = Import(typeof(InitializeOnLoadMethodAttribute));
                initializeOnLoadMethodAttribute = initializeOnLoadMethodAttributeRef.Resolve();
            }

            // [RuntimeInitializeOnLoadMethod]
            TypeReference runtimeInitializeOnLoadMethodAttributeRef = Import(typeof(RuntimeInitializeOnLoadMethodAttribute));
            runtimeInitializeOnLoadMethodAttribute = runtimeInitializeOnLoadMethodAttributeRef.Resolve();
        }
    }
}
