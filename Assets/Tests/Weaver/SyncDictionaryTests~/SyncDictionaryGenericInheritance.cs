using Mirror;

namespace SyncDictionaryTests.SyncDictionaryGenericInheritance
{
    class SyncDictionaryGenericInheritance : NetworkBehaviour
    {
        readonly SomeDictionaryIntString dictionary = new SomeDictionaryIntString();


        public class SomeDictionary<TKey, TItem> : SyncDictionary<TKey, TItem> { }

        public class SomeDictionaryIntString : SomeDictionary<int, string> { }
    }
}
