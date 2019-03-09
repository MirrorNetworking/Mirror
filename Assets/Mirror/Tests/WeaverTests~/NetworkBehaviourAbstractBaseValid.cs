using UnityEngine;
using Mirror;

namespace MirrorTest
{
    public abstract class EntityBase : NetworkBehaviour {}

    public class EntityConcrete : EntityBase
    {
        [SyncVar]
        public int abstractDerivedSync;
    }

    public class EntityTest : EntityConcrete
    {
        [SyncVar]
        public int concreteDerivedSync;
    }
}
