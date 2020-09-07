using Mirror;

namespace WeaverSyncVarTests.SyncVarsSyncList
{

    class SyncVarsSyncList : NetworkBehaviour
    {
        public class SyncObjImplementer : ISyncObject
        {
            public bool IsDirty { get; }
            public void Flush() { }
            public void OnSerializeAll(NetworkWriter writer) { }
            public void OnSerializeDelta(NetworkWriter writer) { }
            public void OnDeserializeAll(NetworkReader reader) { }
            public void OnDeserializeDelta(NetworkReader reader) { }
            public void Reset() { }
        }

        [SyncVar(hook = nameof(OnChangeHealth))]
        int health;

        [SyncVar]
        SyncObjImplementer syncobj;

        [SyncVar]
        SyncListInt syncints;

        public void TakeDamage(int amount)
        {
            if (!IsServer)
                return;

            health -= amount;
        }

        void OnChangeHealth(int oldHealth, int newHealth)
        {
            // do things with your health bar
        }
    }
}
