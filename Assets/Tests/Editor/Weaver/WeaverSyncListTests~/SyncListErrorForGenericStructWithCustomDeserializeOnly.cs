using Mirror;

namespace WeaverSyncListTests.SyncListErrorForGenericStructWithCustomDeserializeOnly
{
    class SyncListErrorForGenericStructWithCustomDeserializeOnly : NetworkBehaviour
    {
        MyGenericStructList harpseals;


        struct MyGenericStruct<T>
        {
            public T genericpotato;
        }

        class MyGenericStructList : SyncList<MyGenericStruct<float>>
        {
            protected override MyGenericStruct<float> DeserializeItem(NetworkReader reader)
            {
                return new MyGenericStruct<float>() { genericpotato = reader.ReadSingle() };
            }
        }
    }
}
