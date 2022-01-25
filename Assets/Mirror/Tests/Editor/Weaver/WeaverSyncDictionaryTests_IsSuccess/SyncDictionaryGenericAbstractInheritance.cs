using Mirror;

namespace WeaverSyncDictionaryTests.SyncDictionaryGenericAbstractInheritance
{
    class SyncDictionaryGenericAbstractInheritance : NetworkBehaviour
    {
        readonly SomeDictionaryIntString dictionary = new SomeDictionaryIntString();

        public abstract class SomeDictionary<TKey, TItem> : SyncDictionary<TKey, TItem> { }

        public class SomeDictionaryIntString : SomeDictionary<int, string> { }
    }
}
