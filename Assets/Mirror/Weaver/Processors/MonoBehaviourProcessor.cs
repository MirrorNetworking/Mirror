
using Mono.Cecil;

namespace Mirror.Weaver
{
    /// <summary>
    /// only shows warnings in case we use SyncVars etc. for MonoBehaviour.
    /// </summary>
    class MonoBehaviourProcessor
    {
        private readonly IWeaverLogger logger;

        public MonoBehaviourProcessor(IWeaverLogger logger)
        {
            this.logger = logger;
        }

        public void Process(TypeDefinition td)
        {
            ProcessSyncVars(td);
            ProcessMethods(td);
        }

        void ProcessSyncVars(TypeDefinition td)
        {
            // find syncvars
            foreach (FieldDefinition fd in td.Fields)
            {
                if (fd.HasCustomAttribute<SyncVarAttribute>())
                    logger.Error($"SyncVar {fd.Name} must be inside a NetworkBehaviour.  {td.Name} is not a NetworkBehaviour", fd);

                if (SyncObjectProcessor.ImplementsSyncObject(fd.FieldType))
                {
                    logger.Error($"{fd.Name} is a SyncObject and must be inside a NetworkBehaviour.  {td.Name} is not a NetworkBehaviour", fd);
                }
            }
        }

        void ProcessMethods(TypeDefinition td)
        {
            // find ServerRpc and RPC functions
            foreach (MethodDefinition md in td.Methods)
            {
                if (md.HasCustomAttribute<ServerRpcAttribute>())
                    logger.Error($"ServerRpc {md.Name} must be declared inside a NetworkBehaviour", md);
                if (md.HasCustomAttribute<ClientRpcAttribute>())
                    logger.Error($"ClientRpc {md.Name} must be declared inside a NetworkBehaviour", md);
                if (md.HasCustomAttribute<ClientAttribute>())
                    logger.Error($"Client method {md.Name} must be declared inside a NetworkBehaviour", md);                        
                if (md.HasCustomAttribute<ServerAttribute>())
                    logger.Error($"Server method {md.Name} must be declared inside a NetworkBehaviour", md);
            }
        }
    }
}
