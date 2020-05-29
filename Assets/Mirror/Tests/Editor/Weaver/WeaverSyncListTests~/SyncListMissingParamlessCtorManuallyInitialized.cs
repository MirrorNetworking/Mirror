using Mirror;

namespace WeaverSyncListTests.SyncListMissingParamlessCtorManuallyInitialized
{
    class SyncListMissingParamlessCtorManuallyInitialized : NetworkBehaviour
    {
        public SyncListString2 Foo = new SyncListString2(20);


        public class SyncListString2 : SyncList<string>
        {
            public SyncListString2(int phooey) { }
            protected override void SerializeItem(NetworkWriter w, string item) { }
            protected override string DeserializeItem(NetworkReader r) => "";
        }
    }
}
