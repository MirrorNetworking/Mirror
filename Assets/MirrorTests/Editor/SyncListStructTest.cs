using NUnit.Framework;

namespace Mirror.Tests
{
    public class SyncListStructTest
    {
        [Test]
        public void ListIsDirtyWhenModifingAndSettingStruct()
        {
            SyncListTestPlayer serverList = new SyncListTestPlayer();
            SyncListTestPlayer clientList = new SyncListTestPlayer();
            SyncListTest.SerializeAllTo(serverList, clientList);
            serverList.Add(new TestPlayer { item = new TestItem { price = 10 } });
            SyncListTest.SerializeDeltaTo(serverList, clientList);
            Assert.That(serverList.IsDirty, Is.False);

            TestPlayer player = serverList[0];
            player.item.price = 15;
            serverList[0] = player;

            Assert.That(serverList.IsDirty, Is.True);
        }


        [Test]
        public void OldValueShouldNotBeNewValue()
        {
            SyncListTestPlayer serverList = new SyncListTestPlayer();
            SyncListTestPlayer clientList = new SyncListTestPlayer();
            SyncListTest.SerializeAllTo(serverList, clientList);
            serverList.Add(new TestPlayer { item = new TestItem { price = 10 } });
            SyncListTest.SerializeDeltaTo(serverList, clientList);

            TestPlayer player = serverList[0];
            player.item.price = 15;
            serverList[0] = player;

            bool callbackCalled = false;
            clientList.Callback += (SyncList<TestPlayer>.Operation op, int itemIndex, TestPlayer oldItem, TestPlayer newItem) =>
            {
                Assert.That(op == SyncList<TestPlayer>.Operation.OP_SET, Is.True);
                Assert.That(itemIndex, Is.EqualTo(0));
                Assert.That(oldItem.item.price, Is.EqualTo(10));
                Assert.That(newItem.item.price, Is.EqualTo(15));
                callbackCalled = true;
            };

            SyncListTest.SerializeDeltaTo(serverList, clientList);
            Assert.IsTrue(callbackCalled);
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
