using Mirror;
using System;

namespace WeaverSyncVarTests.SyncVarsSyncList
{

    class SyncVarsSyncList : NetworkBehaviour
    {
        public class SyncObjImplementer : SyncObject
        {
            public override Action OnDirty { get; set; }
            public override Func<bool> IsRecording { get; set; } = () => true;
            public override void ClearChanges() { }
            public override void OnSerializeAll(NetworkWriter writer) { }
            public override void OnSerializeDelta(NetworkWriter writer) { }
            public override void OnDeserializeAll(NetworkReader reader) { }
            public override void OnDeserializeDelta(NetworkReader reader) { }
            public override void Reset() { }

            // Deprecated 2021-09-17
            [Obsolete("Use ClearChanges instead")]
            public override void Flush() { }
        }

        [SyncVar]
        SyncObjImplementer syncobj;

        [SyncVar]
        SyncList<int> syncints;
    }
}
