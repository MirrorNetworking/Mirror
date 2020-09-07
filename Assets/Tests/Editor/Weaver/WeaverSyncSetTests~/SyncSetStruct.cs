using Mirror;

namespace WeaverSyncSetTests.SyncSetStruct
{
    class SyncSetStruct : NetworkBehaviour
    {
        MyStructSet Foo;

        struct MyStruct
        {
            int potato;
            float floatingpotato;
            double givemetwopotatoes;
        }
        class MyStructSet : SyncHashSet<MyStruct> { }
    }
}
