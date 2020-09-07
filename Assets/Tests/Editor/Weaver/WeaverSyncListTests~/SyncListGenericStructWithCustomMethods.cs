using Mirror;

namespace WeaverSyncListTests.SyncListGenericStructWithCustomMethods
{
    class SyncListGenericStructWithCustomMethods : NetworkBehaviour
    {
        MyGenericStructList harpseals;


        struct MyGenericStruct<T>
        {
            public T genericpotato;
        }

        class MyGenericStructList : SyncList<MyGenericStruct<float>>
        {
            protected override void SerializeItem(NetworkWriter writer, MyGenericStruct<float> item)
            {
                writer.WriteSingle(item.genericpotato);
            }

            protected override MyGenericStruct<float> DeserializeItem(NetworkReader reader)
            {
                return new MyGenericStruct<float>() { genericpotato = reader.ReadSingle() };
            }
        }
    }
}
