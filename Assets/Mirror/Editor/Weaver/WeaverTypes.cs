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

        public MethodReference GetPooledWriterReference;
        public MethodReference RecycleWriterReference;

        public MethodReference NetworkClientConnectionReference;

        public MethodReference RemoteCallDelegateConstructor;

        public MethodReference NetworkServerGetActive;
        public MethodReference NetworkClientGetActive;

        // custom attribute types
        public MethodReference InitSyncObjectReference;

        // array segment
        public MethodReference ArraySegmentConstructorReference;

        // syncvar
        public MethodReference registerCommandReference;
        public MethodReference registerRpcReference;
        public MethodReference getTypeFromHandleReference;
        public MethodReference logErrorReference;
        public MethodReference logWarningReference;
        public MethodReference sendCommandInternal;
        public MethodReference sendRpcInternal;
        public MethodReference sendTargetRpcInternal;

        public MethodReference readNetworkBehaviourGeneric;

        // Action<T,T>
        public TypeReference ActionT_T_Type;
        public MethodReference ActionT_T_GenericConstructor;

        // SyncVar<T> type & explicit GO/NI/NB for persistence through netId
        public TypeReference SyncVarT_Type;
        public TypeReference SyncVarT_GameObject_Type;
        public TypeReference SyncVarT_NetworkIdentity_Type;
        public TypeReference SyncVarT_NetworkBehaviour_Type;

        // SyncVar<T> constructor & explicit GO/NI/NB for persistence through netId
        public MethodReference SyncVarT_GenericConstructor;
        public MethodReference SyncVarT_GameObject_Constructor;
        public MethodReference SyncVarT_NetworkIdentity_Constructor;
        public MethodReference SyncVarT_NetworkBehaviour_Constructor;

        // SyncVar<T>.Value getter & explicit GO/NI/NB for persistence through netId
        public MethodReference SyncVarT_Value_Get_Reference;
        public MethodReference SyncVarT_GameObject_Value_Get_Reference;
        public MethodReference SyncVarT_NetworkIdentity_Value_Get_Reference;
        public MethodReference SyncVarT_NetworkBehaviour_Value_Get_Reference;

        // SyncVar<T>.Value setter & explicit GO/NI/NB for persistence through netId
        public MethodReference SyncVarT_Value_Set_Reference;
        public MethodReference SyncVarT_GameObject_Value_Set_Reference;
        public MethodReference SyncVarT_NetworkIdentity_Value_Set_Reference;
        public MethodReference SyncVarT_NetworkBehaviour_Value_Set_Reference;

        // attributes
        public TypeDefinition initializeOnLoadMethodAttribute;
        public TypeDefinition runtimeInitializeOnLoadMethodAttribute;
        public TypeDefinition hideInInspectorAttribute;

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

            SyncVarT_Type = Import(typeof(SyncVar<>));
            SyncVarT_GameObject_Type = Import(typeof(SyncVarGameObject));
            SyncVarT_NetworkIdentity_Type = Import(typeof(SyncVarNetworkIdentity));
            SyncVarT_NetworkBehaviour_Type = Import(typeof(SyncVarNetworkBehaviour<>));

            SyncVarT_GenericConstructor = Resolvers.ResolveMethod(SyncVarT_Type, assembly, Log, ".ctor", ref WeavingFailed);
            SyncVarT_GameObject_Constructor = Resolvers.ResolveMethod(SyncVarT_GameObject_Type, assembly, Log, ".ctor", ref WeavingFailed);
            SyncVarT_NetworkIdentity_Constructor = Resolvers.ResolveMethod(SyncVarT_NetworkIdentity_Type, assembly, Log, ".ctor", ref WeavingFailed);
            SyncVarT_NetworkBehaviour_Constructor = Resolvers.ResolveMethod(SyncVarT_NetworkBehaviour_Type, assembly, Log, ".ctor", ref WeavingFailed);

            SyncVarT_Value_Get_Reference = Resolvers.ResolveMethod(SyncVarT_Type, assembly, Log, "get_Value", ref WeavingFailed);
            SyncVarT_GameObject_Value_Get_Reference = Resolvers.ResolveMethod(SyncVarT_GameObject_Type, assembly, Log, "get_Value", ref WeavingFailed);
            SyncVarT_NetworkIdentity_Value_Get_Reference = Resolvers.ResolveMethod(SyncVarT_NetworkIdentity_Type, assembly, Log, "get_Value", ref WeavingFailed);
            SyncVarT_NetworkBehaviour_Value_Get_Reference = Resolvers.ResolveMethod(SyncVarT_NetworkBehaviour_Type, assembly, Log, "get_Value", ref WeavingFailed);

            SyncVarT_Value_Set_Reference = Resolvers.ResolveMethod(SyncVarT_Type, assembly, Log, "set_Value", ref WeavingFailed);
            SyncVarT_GameObject_Value_Set_Reference = Resolvers.ResolveMethod(SyncVarT_GameObject_Type, assembly, Log, "set_Value", ref WeavingFailed);
            SyncVarT_NetworkIdentity_Value_Set_Reference = Resolvers.ResolveMethod(SyncVarT_NetworkIdentity_Type, assembly, Log, "set_Value", ref WeavingFailed);
            SyncVarT_NetworkBehaviour_Value_Set_Reference = Resolvers.ResolveMethod(SyncVarT_NetworkBehaviour_Type, assembly, Log, "set_Value", ref WeavingFailed);

            ActionT_T_Type = Import(typeof(Action<,>));
            ActionT_T_GenericConstructor = Resolvers.ResolveMethod(ActionT_T_Type, assembly, Log, ".ctor", ref WeavingFailed);

            TypeReference NetworkServerType = Import(typeof(NetworkServer));
            NetworkServerGetActive = Resolvers.ResolveMethod(NetworkServerType, assembly, Log, "get_active", ref WeavingFailed);
            TypeReference NetworkClientType = Import(typeof(NetworkClient));
            NetworkClientGetActive = Resolvers.ResolveMethod(NetworkClientType, assembly, Log, "get_active", ref WeavingFailed);

            TypeReference RemoteCallDelegateType = Import<RemoteCalls.RemoteCallDelegate>();
            RemoteCallDelegateConstructor = Resolvers.ResolveMethod(RemoteCallDelegateType, assembly, Log, ".ctor", ref WeavingFailed);

            TypeReference NetworkBehaviourType = Import<NetworkBehaviour>();
            TypeReference RemoteProcedureCallsType = Import(typeof(RemoteCalls.RemoteProcedureCalls));

            TypeReference ScriptableObjectType = Import<UnityEngine.ScriptableObject>();

            ScriptableObjectCreateInstanceMethod = Resolvers.ResolveMethod(
                ScriptableObjectType, assembly, Log,
                md => md.Name == "CreateInstance" && md.HasGenericParameters,
                ref WeavingFailed);

            TypeReference NetworkWriterPoolType = Import(typeof(NetworkWriterPool));
            GetPooledWriterReference = Resolvers.ResolveMethod(NetworkWriterPoolType, assembly, Log, "GetWriter", ref WeavingFailed);
            RecycleWriterReference = Resolvers.ResolveMethod(NetworkWriterPoolType, assembly, Log, "Recycle", ref WeavingFailed);

            NetworkClientConnectionReference = Resolvers.ResolveMethod(NetworkClientType, assembly, Log, "get_connection", ref WeavingFailed);

            registerCommandReference = Resolvers.ResolveMethod(RemoteProcedureCallsType, assembly, Log, "RegisterCommand", ref WeavingFailed);
            registerRpcReference = Resolvers.ResolveMethod(RemoteProcedureCallsType, assembly, Log, "RegisterRpc", ref WeavingFailed);

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

            // [HideInInspector]
            TypeReference hideInInspectorAttributeRef = Import(typeof(HideInInspector));
            hideInInspectorAttribute = hideInInspectorAttributeRef.Resolve();
        }
    }
}
