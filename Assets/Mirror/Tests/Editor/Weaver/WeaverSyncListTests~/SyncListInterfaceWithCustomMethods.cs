using Mirror;

namespace WeaverSyncListTests.SyncListInterfaceWithCustomMethods
{
    class SyncListInterfaceWithCustomMethods : NetworkBehaviour
    {
        MyInterfaceList Foo;
    }

    interface IMyInterface
    {
        int someNumber { get; set; }
    }

    class MyUser : IMyInterface
    {
        public int someNumber { get; set; }
    }

    class MyInterfaceList : SyncList<IMyInterface>
    {
        protected override void SerializeItem(NetworkWriter writer, IMyInterface item)
        {
            writer.WriteInt32(item.someNumber);
        }
        protected override IMyInterface DeserializeItem(NetworkReader reader)
        {
            return new MyUser { someNumber = reader.ReadInt32() };
        }
    }
}
