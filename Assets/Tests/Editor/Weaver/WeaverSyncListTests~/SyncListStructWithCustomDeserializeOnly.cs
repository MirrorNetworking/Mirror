using Mirror;

namespace WeaverSyncListTests.SyncListStructWithCustomDeserializeOnly
{
    class SyncListStructWithCustomDeserializeOnly : NetworkBehaviour
    {
        MyStructList Foo;

        struct MyStruct
        {
            public int potato;
            public float floatingpotato;
            public double givemetwopotatoes;
        }
        class MyStructList : SyncList<MyStruct>
        {
            protected override MyStruct DeserializeItem(NetworkReader reader)
            {
                return new MyStruct() { /* read some stuff here */ };
            }
        }
    }
}
