using System;
using System.Collections.Generic;
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
        public void TestFind()
        {
            string element = serverSyncList.Find(entry => entry == "World");
            Assert.That(element, Is.EqualTo("World"));
        }

        [Test]
        public void TestNoFind()
        {
            string nonexistent = serverSyncList.Find(entry => entry == "yay");
            Assert.That(nonexistent, Is.Null);
        }

        [Test]
        public void TestFindAll()
        {
            List<string> results = serverSyncList.FindAll(entry => entry.Contains("l"));
            Assert.That(results, Is.EquivalentTo(new[] { "Hello", "World" }));
        }

        [Test]
        public void TestFindAllNonExistent()
        {
            List<string> nonexistent = serverSyncList.FindAll(entry => entry == "yay");
            Assert.That(nonexistent, Is.Empty);
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

        [Test]
        public void ResetTest()
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

            clientList.Reset();

            // make old client the host

            SyncListUInt hostList = clientList;
            SyncListUInt clientList2 = new SyncListUInt();

            Assert.That(hostList.IsReadOnly, Is.False);
            Assert.That(clientList2.IsReadOnly, Is.False);

            hostList.Add(1U);
            hostList.Add(2U);
            hostList.Add(3U);
            SerializeDeltaTo(hostList, clientList2);

            // client list should now lock itself,  trying to modify it
            // should produce an InvalidOperationException
            Assert.That(clientList2.IsReadOnly, Is.True);
            Assert.That(hostList.IsReadOnly, Is.False);
            Assert.Throws<InvalidOperationException>(() => { clientList2.Add(5U); });
        }

        [Test]
        public void ResetShouldClearChanges()
        {
            SyncListUInt serverList = new SyncListUInt();

            serverList.Add(1U);
            serverList.Add(2U);
            serverList.Add(3U);

            Assert.That(serverList.GetChangeCount(), Is.GreaterThan(0));

            serverList.Reset();

            Assert.That(serverList.GetChangeCount(), Is.Zero);
        }

        [Test]
        public void ResetShouldClearItems()
        {
            SyncListUInt serverList = new SyncListUInt();

            serverList.Add(1U);
            serverList.Add(2U);
            serverList.Add(3U);

            Assert.That(serverList.Count, Is.GreaterThan(0));

            serverList.Reset();

            Assert.That(serverList.Count, Is.Zero);
        }
    }

    public static class SyncObjectTestMethods
    {
        public static uint GetChangeCount(this SyncObject syncObject)
        {
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                syncObject.OnSerializeDelta(writer);

                using (PooledNetworkReader reader = NetworkReaderPool.GetReader(writer.ToArraySegment()))
                {
                    uint count = reader.ReadPackedUInt32();

                    return count;
                }
            }
        }
    }
}
