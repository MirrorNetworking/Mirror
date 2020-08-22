using Mirror;

namespace WeaverSyncListTests.SyncListStructWithCustomMethods
{
    class SyncListStructWithCustomMethods : NetworkBehaviour
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
            protected override void SerializeItem(NetworkWriter writer, MyStruct item)
            {
                // write some stuff here
            }

            protected override MyStruct DeserializeItem(NetworkReader reader)
            {
                return new MyStruct() { /* read some stuff here */ };
            }
        }
    }
}
