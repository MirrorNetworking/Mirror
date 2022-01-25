using Mirror;

namespace WeaverSyncDictionaryTests.GenericSyncDictionaryCanBeUsed
{
    class GenericSyncDictionaryCanBeUsed : NetworkBehaviour
    {
        readonly SomeSyncDictionary<int, string> someDictionary;

        public class SomeSyncDictionary<TKey, TItem> : SyncDictionary<TKey, TItem> { }
    }
}
