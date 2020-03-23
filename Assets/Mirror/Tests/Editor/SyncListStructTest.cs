using NUnit.Framework;

namespace Mirror.Tests
{
    public class SyncListStructTest
    {
        [Test]
        public void ListIsDirtyWhenModifingAndSettingStruct()
        {
            var serverList = new SyncListTestPlayer();
            var clientList = new SyncListTestPlayer();
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


    public class SyncListTestPlayer : SyncList<TestPlayer>
    {

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
