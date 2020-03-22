using NUnit.Framework;

namespace Mirror.Tests
{
    public class SyncListStrucTest
    {
        [Test]
        [Ignore("This test is to show that List/Item is not dirty after seting a field inside Item")]
        public void ListNotDirtyAfterChangingValueInsideItem()
        {
            SyncListTestPlayer serverList = new SyncListTestPlayer();
            SyncListTestPlayer clientList = new SyncListTestPlayer();
            SyncListTest.SerializeAllTo(serverList, clientList);
            serverList.Add(new TestPlayer { armor = new TestArmor() { weight = 10 } });
            SyncListTest.SerializeDeltaTo(serverList, clientList);
            Assert.That(serverList.IsDirty, Is.False);

            serverList.Callback += (SyncList<TestPlayer>.Operation op, int itemIndex, TestPlayer oldItem, TestPlayer newItem) =>
            {
                // see Ignore reason
                Assert.Fail();
            };
            serverList[0].armor.weight = 15;

            Assert.That(serverList.IsDirty, Is.False);
            SyncListTest.SerializeDeltaTo(serverList, clientList);
        }

        [Test]
        public void ListIsDirtAfterCallingSetItemDirty()
        {
            SyncListTestPlayer serverList = new SyncListTestPlayer();
            SyncListTestPlayer clientList = new SyncListTestPlayer();
            SyncListTest.SerializeAllTo(serverList, clientList);
            serverList.Add(new TestPlayer { armor = new TestArmor() { weight = 10 } });
            SyncListTest.SerializeDeltaTo(serverList, clientList);

            Assert.That(serverList.IsDirty, Is.False);

            serverList[0].armor.weight = 15;

            Assert.That(serverList.IsDirty, Is.False);

            serverList.SetItemDirty(0);

            Assert.That(serverList.IsDirty, Is.True);
        }


        [Test]
        public void OldValueShouldNotBeNewValue()
        {
            SyncListTestPlayer serverList = new SyncListTestPlayer();
            SyncListTestPlayer clientList = new SyncListTestPlayer();
            SyncListTest.SerializeAllTo(serverList, clientList);
            serverList.Add(new TestPlayer { armor = new TestArmor { weight = 10 } });
            SyncListTest.SerializeDeltaTo(serverList, clientList);

            serverList[0].armor.weight = 15;
            serverList.SetItemDirty(0);

            clientList.Callback += ClientList_Callback;
            SyncListTest.SerializeDeltaTo(serverList, clientList);
        }

        static void ClientList_Callback(SyncList<TestPlayer>.Operation op, int itemIndex, TestPlayer oldItem, TestPlayer newItem)
        {
            Assert.That(op == SyncList<TestPlayer>.Operation.OP_SET, Is.True);
            Assert.That(itemIndex, Is.EqualTo(0));
            Assert.That(oldItem.armor.weight, Is.EqualTo(10));
            Assert.That(newItem.armor.weight, Is.EqualTo(15));
        }
    }


    public class SyncListTestPlayer : SyncList<TestPlayer>
    {

    }
    public struct TestPlayer
    {
        public TestArmor armor;
    }
    public class TestArmor
    {
        public float weight;
    }
    public static class TestItemReadWrite
    {
        public static void WriteTestItem(this NetworkWriter writer, TestArmor armor)
        {
            writer.WriteSingle(armor.weight);
        }
        public static TestArmor ReadTestItem(this NetworkReader reader)
        {
            return new TestArmor { weight = reader.ReadSingle() };
        }
    }
}
