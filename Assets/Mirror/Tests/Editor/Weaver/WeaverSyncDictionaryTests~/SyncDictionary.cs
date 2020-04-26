using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class SyncDictionaryValid : NetworkBehaviour
    {
        public class SyncDictionaryIntString : SyncDictionary<int, string> { }
        
        public SyncDictionaryIntString Foo;
    }

    
}
