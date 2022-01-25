using Mirror;

namespace WeaverSyncListTests.SyncListMissingParamlessCtor
{
    class SyncListMissingParamlessCtor : NetworkBehaviour
    {
        public readonly SyncListString2 Foo;

        public class SyncListString2 : SyncList<string>
        {
            public SyncListString2(int phooey) { }
        }
    }
}
