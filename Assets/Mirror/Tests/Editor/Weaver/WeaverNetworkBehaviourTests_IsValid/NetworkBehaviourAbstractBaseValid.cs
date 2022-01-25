using Mirror;

namespace WeaverNetworkBehaviourTests.NetworkBehaviourAbstractBaseValid
{
    public abstract class EntityBase : NetworkBehaviour { }

    public class EntityConcrete : EntityBase
    {
        [SyncVar]
        public int abstractDerivedSync;
    }

    public class NetworkBehaviourAbstractBaseValid : EntityConcrete
    {
        [SyncVar]
        public int concreteDerivedSync;
    }
}
