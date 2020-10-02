using Mirror;

namespace WeaverSyncListTests.SyncListInterfaceWithCustomMethods
{
    class SyncListInterfaceWithCustomMethods : NetworkBehaviour
    {
        SyncList<IMyInterface> Foo;
    }

    interface IMyInterface
    {
        int someNumber { get; set; }
    }

    class MyUser : IMyInterface
    {
        public int someNumber { get; set; }
    }

    static class MyInterfaceList
    {
        static void SerializeItem(this NetworkWriter writer, IMyInterface item)
        {
            writer.WriteInt32(item.someNumber);
        }
        static IMyInterface DeserializeItem(this NetworkReader reader)
        {
            return new MyUser { someNumber = reader.ReadInt32() };
        }
    }
}
