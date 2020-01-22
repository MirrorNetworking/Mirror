using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        public class SyncListString2 : SyncList<string>
        {
            public SyncListString2(int phooey) {}
            protected override void SerializeItem(NetworkWriter w, string item) {}
            protected override string DeserializeItem(NetworkReader r) => "";
        }

        public SyncListString2 Foo;
    }
}
