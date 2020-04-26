using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.SyncDictionary
{
    class SyncDictionaryValid : NetworkBehaviour
    {
        public class SyncDictionaryIntString : SyncDictionary<int, string> { }
        
        public SyncDictionaryIntString Foo;
    }

    
}
