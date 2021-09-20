using Mirror;
using System;

namespace WeaverSyncVarTests.SyncVarsSyncList
{

    class SyncVarsSyncList : NetworkBehaviour
    {
        public class SyncObjImplementer : SyncObject
        {
            public Action OnDirty { get; set; }
            public Func<bool> IsRecording { get; set; } = () => true;
            public void ClearChanges() { }
            public void OnSerializeAll(NetworkWriter writer) { }
            public void OnSerializeDelta(NetworkWriter writer) { }
            public void OnDeserializeAll(NetworkReader reader) { }
            public void OnDeserializeDelta(NetworkReader reader) { }
            public void Reset() { }

            // Deprecated 2021-09-17
            [Obsolete("Use ClearChanges instead")]
            public void Flush() { }
        }

        [SyncVar]
        SyncObjImplementer syncobj;

        [SyncVar]
        SyncList<int> syncints;
    }
}
