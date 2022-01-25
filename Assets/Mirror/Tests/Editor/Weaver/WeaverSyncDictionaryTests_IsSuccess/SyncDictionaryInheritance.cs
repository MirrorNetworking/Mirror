using Mirror;

namespace WeaverSyncDictionaryTests.SyncDictionaryInheritance
{
    class SyncDictionaryInheritance : NetworkBehaviour
    {
        readonly SuperDictionary dictionary = new SuperDictionary();

        public class SomeDictionary<TKey, TItem> : SyncDictionary<TKey, TItem> { }

        public class SomeDictionaryIntString : SomeDictionary<int, string> { }

        public class SuperDictionary : SomeDictionaryIntString
        {
        }
    }
}
