using Mirror;

namespace WeaverSyncDictionaryTests.SyncDictionary
{
    class SyncDictionaryValid : NetworkBehaviour
    {
        public class SyncDictionaryIntString : SyncDictionary<int, string> { }

        public SyncDictionaryIntString Foo;
    }


}
