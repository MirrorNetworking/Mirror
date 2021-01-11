using Mirror;

namespace WeaverSyncDictionaryTests.SyncDictionaryErrorWhenUsingGenericInNetworkBehaviour
{
    class SyncDictionaryErrorWhenUsingGenericInNetworkBehaviour : NetworkBehaviour
    {
        readonly SomeSyncDictionary<int, string> someDictionary;


        public class SomeSyncDictionary<TKey, TItem> : SyncDictionary<TKey, TItem> { }
    }
}
