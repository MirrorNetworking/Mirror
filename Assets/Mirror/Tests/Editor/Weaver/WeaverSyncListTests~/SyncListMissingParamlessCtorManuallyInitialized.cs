using Mirror;

namespace WeaverSyncListTests.SyncListMissingParamlessCtorManuallyInitialized
{
    class SyncListMissingParamlessCtorManuallyInitialized : NetworkBehaviour
    {
        public readonly SyncListString2 Foo = new SyncListString2(20);

        public class SyncListString2 : SyncList<string>
        {
            public SyncListString2(int phooey) { }
        }
    }
}
