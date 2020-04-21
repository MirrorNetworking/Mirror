using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class SyncDictionaryValid : NetworkBehaviour
    {
        public SyncDictionaryIntString Foo;
    }

    public class SyncDictionaryIntString : SyncDictionary<int, string> { }
}
