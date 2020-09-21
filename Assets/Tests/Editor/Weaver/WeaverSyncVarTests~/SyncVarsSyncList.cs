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

        [SyncVar]
        SyncObjImplementer syncobj;

        [SyncVar]
        SyncListInt syncints;
    }
}
