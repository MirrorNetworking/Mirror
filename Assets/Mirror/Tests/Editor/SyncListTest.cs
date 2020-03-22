using System;
using NUnit.Framework;

namespace Mirror.Tests
{
    [TestFixture]
    public class SyncListTest
    {
        SyncListString serverSyncList;
        SyncListString clientSyncList;

        public static void SerializeAllTo<T>(T fromList, T toList) where T : SyncObject
        {
            NetworkWriter writer = new NetworkWriter();
            fromList.OnSerializeAll(writer);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            toList.OnDeserializeAll(reader);
        }

        public static void SerializeDeltaTo<T>(T fromList, T toList) where T : SyncObject
        {
            NetworkWriter writer = new NetworkWriter();
            fromList.OnSerializeDelta(writer);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            toList.OnDeserializeDelta(reader);
            fromList.Flush();
        }

        [SetUp]
        public void SetUp()
        {
            serverSyncList = new SyncListString();
            clientSyncList = new SyncListString();

            // add some data to the list
            serverSyncList.Add("Hello");
            serverSyncList.Add("World");
            serverSyncList.Add("!");
            SerializeAllTo(serverSyncList, clientSyncList);
        }

        [Test]
        public void TestInit()
        {
            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "World", "!" }));
        }

        [Test]
        public void TestAdd()
        {
            serverSyncList.Add("yay");
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "World", "!", "yay" }));
        }

        [Test]
        public void TestClear()
        {
            serverSyncList.Clear();
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList, Is.EquivalentTo(new string[] { }));
        }

        [Test]
        public void TestInsert()
        {
            serverSyncList.Insert(0, "yay");
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "yay", "Hello", "World", "!" }));
        }

        [Test]
        public void TestSet()
        {
            serverSyncList[1] = "yay";
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList[1], Is.EqualTo("yay"));
            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "yay", "!" }));
        }

        [Test]
        public void TestSetNull()
        {
            serverSyncList[1] = null;
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList[1], Is.EqualTo(null));
            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", null, "!" }));
            serverSyncList[1] = "yay";
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "yay", "!" }));
        }

        [Test]
        public void TestRemoveAt()
        {
            serverSyncList.RemoveAt(1);
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "!" }));
        }

        [Test]
        public void TestRemove()
        {
            serverSyncList.Remove("World");
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "!" }));
        }

        [Test]
        public void TestFindIndex()
        {
            int index = serverSyncList.FindIndex(entry => entry == "World");
            Assert.That(index, Is.EqualTo(1));
        }

        [Test]
        public void TestMultSync()
        {
            serverSyncList.Add("1");
            SerializeDeltaTo(serverSyncList, clientSyncList);
            // add some delta and see if it applies
            serverSyncList.Add("2");
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "World", "!", "1", "2" }));
        }

        [Test]
        public void SyncListIntTest()
        {
            SyncListInt serverList = new SyncListInt();
            SyncListInt clientList = new SyncListInt();

            serverList.Add(1);
            serverList.Add(2);
            serverList.Add(3);
            SerializeDeltaTo(serverList, clientList);

            Assert.That(clientList, Is.EquivalentTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void SyncListBoolTest()
        {
            SyncListBool serverList = new SyncListBool();
            SyncListBool clientList = new SyncListBool();

            serverList.Add(true);
            serverList.Add(false);
            serverList.Add(true);
            SerializeDeltaTo(serverList, clientList);

            Assert.That(clientList, Is.EquivalentTo(new[] { true, false, true }));
        }

        [Test]
        public void SyncListUintTest()
        {
            SyncListUInt serverList = new SyncListUInt();
            SyncListUInt clientList = new SyncListUInt();

            serverList.Add(1U);
            serverList.Add(2U);
            serverList.Add(3U);
            SerializeDeltaTo(serverList, clientList);

            Assert.That(clientList, Is.EquivalentTo(new[] { 1U, 2U, 3U }));
        }

        [Test]
        public void SyncListFloatTest()
        {
            SyncListFloat serverList = new SyncListFloat();
            SyncListFloat clientList = new SyncListFloat();

            serverList.Add(1.0F);
            serverList.Add(2.0F);
            serverList.Add(3.0F);
            SerializeDeltaTo(serverList, clientList);

            Assert.That(clientList, Is.EquivalentTo(new[] { 1.0F, 2.0F, 3.0F }));
        }

        [Test]
        public void CallbackTest()
        {
            bool called = false;

            clientSyncList.Callback += (op, index, oldItem, newItem) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_ADD));
                Assert.That(index, Is.EqualTo(3));
                Assert.That(oldItem, Is.EqualTo(default(string)));
                Assert.That(newItem, Is.EqualTo("yay"));
            };

            serverSyncList.Add("yay");
            SerializeDeltaTo(serverSyncList, clientSyncList);

            Assert.That(called, Is.True);
        }

        [Test]
        public void CallbackRemoveTest()
        {
            bool called = false;

            clientSyncList.Callback += (op, index, oldItem, newItem) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_REMOVEAT));
                Assert.That(oldItem, Is.EqualTo("World"));
                Assert.That(newItem, Is.EqualTo(default(string)));
            };
            serverSyncList.Remove("World");
            SerializeDeltaTo(serverSyncList, clientSyncList);

            Assert.That(called, Is.True);
        }

        [Test]
        public void CallbackRemoveAtTest()
        {
            bool called = false;

            clientSyncList.Callback += (op, index, oldItem, newItem) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_REMOVEAT));
                Assert.That(index, Is.EqualTo(1));
                Assert.That(oldItem, Is.EqualTo("World"));
                Assert.That(newItem, Is.EqualTo(default(string)));
            };

            serverSyncList.RemoveAt(1);
            SerializeDeltaTo(serverSyncList, clientSyncList);

            Assert.That(called, Is.True);
        }

        [Test]
        public void CountTest()
        {
            Assert.That(serverSyncList.Count, Is.EqualTo(3));
        }

        [Test]
        public void ReadOnlyTest()
        {
            Assert.That(serverSyncList.IsReadOnly, Is.False);
        }

        [Test]
        public void DirtyTest()
        {
            SyncListInt serverList = new SyncListInt();
            SyncListInt clientList = new SyncListInt();

            // nothing to send
            Assert.That(serverList.IsDirty, Is.False);

            // something has changed
            serverList.Add(1);
            Assert.That(serverList.IsDirty, Is.True);
            SerializeDeltaTo(serverList, clientList);

            // data has been flushed,  should go back to clear
            Assert.That(serverList.IsDirty, Is.False);
        }

        [Test]
        public void ReadonlyTest()
        {
            SyncListUInt serverList = new SyncListUInt();
            SyncListUInt clientList = new SyncListUInt();

            // data has been flushed,  should go back to clear
            Assert.That(clientList.IsReadOnly, Is.False);

            serverList.Add(1U);
            serverList.Add(2U);
            serverList.Add(3U);
            SerializeDeltaTo(serverList, clientList);

            // client list should now lock itself,  trying to modify it
            // should produce an InvalidOperationException
            Assert.That(clientList.IsReadOnly, Is.True);
            Assert.Throws<InvalidOperationException>(() => { clientList.Add(5U); });

        }
    }

    public class SyncListStrucTest
    {
        [Test]
        [Ignore("This test is to show that List/Item is not dirty after seting a field inside Item")]
        public void ListNotDirtyAfterChangingValueInsideItem()
        {
            SyncListTestPlayer serverList = new SyncListTestPlayer();
            SyncListTestPlayer clientList = new SyncListTestPlayer();
            SyncListTest.SerializeAllTo(serverList, clientList);
            serverList.Add(new TestPlayer { id = 1, armor = new TestArmor() { weight = 10 } });
            SyncListTest.SerializeDeltaTo(serverList, clientList);
            Assert.That(serverList.IsDirty, Is.False);

            serverList.Callback += (SyncList<TestPlayer>.Operation op, int itemIndex, TestPlayer oldItem, TestPlayer newItem) =>
            {
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
            serverList.Add(new TestPlayer { id = 1, armor = new TestArmor() { weight = 10 } });
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
            serverList.Add(new TestPlayer { id = 1, armor = new TestArmor { weight = 10 } });
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
        public int id;
        public TestArmor armor;
    }
    public class TestItem
    {
    }
    public class TestArmor : TestItem
    {
        public float weight;
    }
    public static class TestItemReadWrite
    {
        public static void WriteTestItem(this NetworkWriter writer, TestItem item)
        {
            if (item is TestArmor armor)
            {
                writer.WriteByte(1);
                writer.WriteSingle(armor.weight);
            }
            else
            {
                writer.WriteByte(0);
            }
        }
        public static TestItem ReadTestItem(this NetworkReader reader)
        {
            byte id = reader.ReadByte();
            switch (id)
            {
                case 1:
                    return new TestArmor { weight = reader.ReadSingle() };
                //Item.
                case 0:
                default:
                    return new TestItem();
            }
        }
    }
}
