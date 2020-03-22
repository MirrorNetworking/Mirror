using NUnit.Framework;

namespace Mirror.Tests
{
    public class SyncListClassTest
    {
        [Test]
        [Ignore("This test is to show that List/Item is not dirty after seting a field inside Item")]
        public void ListNotDirtyAfterChangingValueInsideItem()
        {
            SyncListTestArmor serverList = new SyncListTestArmor();
            SyncListTestArmor clientList = new SyncListTestArmor();
            SyncListTest.SerializeAllTo(serverList, clientList);
            serverList.Add(new TestArmor { weight = 10 });
            SyncListTest.SerializeDeltaTo(serverList, clientList);
            Assert.That(serverList.IsDirty, Is.False);

            serverList.Callback += (SyncList<TestArmor>.Operation op, int itemIndex, TestArmor oldItem, TestArmor newItem) =>
            {
                // see Ignore reason
                Assert.Fail();
            };
            serverList[0].weight = 15;

            Assert.That(serverList.IsDirty, Is.False);
        }

        [Test]
        public void ListIsDirtAfterCallingSetItemDirty()
        {
            SyncListTestArmor serverList = new SyncListTestArmor();
            SyncListTestArmor clientList = new SyncListTestArmor();
            SyncListTest.SerializeAllTo(serverList, clientList);
            serverList.Add(new TestArmor { weight = 10 });
            SyncListTest.SerializeDeltaTo(serverList, clientList);

            Assert.That(serverList.IsDirty, Is.False);

            serverList[0].weight = 15;

            Assert.That(serverList.IsDirty, Is.False);

            serverList.SetItemDirty(0);

            Assert.That(serverList.IsDirty, Is.True);
        }


        [Test]
        public void OldValueShouldNotBeNewValue()
        {
            SyncListTestArmor serverList = new SyncListTestArmor();
            SyncListTestArmor clientList = new SyncListTestArmor();
            SyncListTest.SerializeAllTo(serverList, clientList);
            serverList.Add(new TestArmor { weight = 10 });
            SyncListTest.SerializeDeltaTo(serverList, clientList);

            serverList[0].weight = 15;
            serverList.SetItemDirty(0);

            clientList.Callback += ClientList_Callback;
            SyncListTest.SerializeDeltaTo(serverList, clientList);
        }

        static void ClientList_Callback(SyncList<TestArmor>.Operation op, int itemIndex, TestArmor oldItem, TestArmor newItem)
        {
            Assert.That(op == SyncList<TestArmor>.Operation.OP_SET, Is.True);
            Assert.That(itemIndex, Is.EqualTo(0));
            Assert.That(oldItem.weight, Is.EqualTo(10));
            Assert.That(newItem.weight, Is.EqualTo(15));
        }
    }


    public class SyncListTestArmor : SyncList<TestArmor>
    {

    }
    public class TestArmor
    {
        public float weight;
    }
    public static class TestArmorReadWrite
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
