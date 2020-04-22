using Mirror;

namespace MirrorTest
{
    class SyncListErrorForGenericStructWithCustomSerializeOnly : NetworkBehaviour
    {
        MyGenericStructList harpseals;
    }

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
    };
}
