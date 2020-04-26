using Mirror;

namespace SyncListStructWithCustomSerializeOnly
{
    class MyBehaviour : NetworkBehaviour
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
        protected override void SerializeItem(NetworkWriter writer, MyStruct item)
        {
            // write some stuff here
        }
    }
}
