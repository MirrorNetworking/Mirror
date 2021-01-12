using Mirror;

namespace SyncDictionaryTests.SyncDictionary
{
    class SyncDictionaryValid : NetworkBehaviour
    {
        public class SyncDictionaryIntString : SyncDictionary<int, string> { }

        public SyncDictionaryIntString Foo;
    }


}
