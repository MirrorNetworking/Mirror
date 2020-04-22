using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class SyncListStructWithCustomDeserializeOnly : NetworkBehaviour
    {
        MyStructList Foo;
    }
    struct MyStruct
    {
        int potato;
        float floatingpotato;
        double givemetwopotatoes;
    }
    class MyStructList : SyncList<MyStruct> 
    {
        protected override MyStruct DeserializeItem(NetworkReader reader)
        {
            return new MyStruct() { /* read some stuff here */ };
        }
    }
}
