using UnityEngine;
using Mirror;

namespace MirrorTest
{
    public class SyncObjImplementer : SyncObject
    {
        public bool IsDirty { get; }
        public void Flush() {}
        public void OnSerializeAll(NetworkWriter writer) {}
        public void OnSerializeDelta(NetworkWriter writer) {}
        public void OnDeserializeAll(NetworkReader reader) {}
        public void OnDeserializeDelta(NetworkReader reader) {}
    }

    class MirrorTestPlayer : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnChangeHealth))]
        int health;

        [SyncVar]
        SyncObjImplementer syncobj;

        [SyncVar]
        SyncListInt syncints;

        public void TakeDamage(int amount)
        {
            if (!isServer)
                return;

            health -= amount;
        }

        void OnChangeHealth(int health)
        {
            // do things with your health bar
        }
    }
}
