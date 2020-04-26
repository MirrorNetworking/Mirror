using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.SyncListMissingParamlessCtor
{
    class SyncListMissingParamlessCtor : NetworkBehaviour
    {
        public SyncListString2 Foo;
    

        public class SyncListString2 : SyncList<string>
        {
            public SyncListString2(int phooey) {}
            protected override void SerializeItem(NetworkWriter w, string item) {}
            protected override string DeserializeItem(NetworkReader r) => "";
        }
    }
}
