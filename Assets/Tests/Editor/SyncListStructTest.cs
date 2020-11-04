using NUnit.Framework;

namespace Mirror.Tests
{
    public class SyncListStructTest
    {
        [Test]
        public void ListIsDirtyWhenModifingAndSettingStruct()
        {
            // let the weaver know to generate a reader and writer for TestPlayer
            NetworkWriter writer = new NetworkWriter() ;
            writer.Write<TestPlayer>(default);

            var serverList = new SyncList<TestPlayer>();
            var clientList = new SyncList<TestPlayer>();
            SyncListTest.SerializeAllTo(serverList, clientList);
            serverList.Add(new TestPlayer { item = new TestItem { price = 10 } });
            SyncListTest.SerializeDeltaTo(serverList, clientList);
            Assert.That(serverList.IsDirty, Is.False);

            TestPlayer player = serverList[0];
            player.item.price = 15;
            serverList[0] = player;

            Assert.That(serverList.IsDirty, Is.True);
        }
    }

    public struct TestPlayer
    {
        public TestItem item;
    }
    public struct TestItem
    {
        public float price;
    }
}
