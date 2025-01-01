using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Mirror.Tests.SyncCollections
{
    [TestFixture]
    public class SyncListTest
    {
        SyncList<string> serverSyncList;
        SyncList<string> clientSyncList;
        int serverSyncListDirtyCalled;
        int clientSyncListDirtyCalled;

        public static void SerializeAllTo<T>(T fromList, T toList) where T : SyncObject
        {
            NetworkWriter writer = new NetworkWriter();
            fromList.OnSerializeAll(writer);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            toList.OnDeserializeAll(reader);

            int writeLength = writer.Position;
            int readLength = reader.Position;
            Assert.That(writeLength == readLength, $"OnSerializeAll and OnDeserializeAll calls write the same amount of data\n    writeLength={writeLength}\n    readLength={readLength}");

        }

        public static void SerializeDeltaTo<T>(T fromList, T toList) where T : SyncObject
        {
            NetworkWriter writer = new NetworkWriter();
            fromList.OnSerializeDelta(writer);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            toList.OnDeserializeDelta(reader);
            fromList.ClearChanges();

            int writeLength = writer.Position;
            int readLength = reader.Position;
            Assert.That(writeLength == readLength, $"OnSerializeDelta and OnDeserializeDelta calls write the same amount of data\n    writeLength={writeLength}\n    readLength={readLength}");
        }

        [SetUp]
        public void SetUp()
        {
            serverSyncList = new SyncList<string>();
            clientSyncList = new SyncList<string>();

            // set writable
            serverSyncList.IsWritable = () => true;
            clientSyncList.IsWritable = () => false;

            // add some data to the list
            serverSyncList.Add("Hello");
            serverSyncList.Add("World");
            serverSyncList.Add("!");
            SerializeAllTo(serverSyncList, clientSyncList);

            // set up dirty callbacks for testing
            // AFTER adding the example data. we already know we added that data.
            serverSyncList.OnDirty = () => ++serverSyncListDirtyCalled;
            clientSyncList.OnDirty = () => ++clientSyncListDirtyCalled;
            serverSyncListDirtyCalled = 0;
            clientSyncListDirtyCalled = 0;
        }

        [Test]
        public void TestInit()
        {
            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "World", "!" }));
        }

        // test the '= List<int>{1,2,3}' constructor.
        // it calls .Add(1); .Add(2); .Add(3) in the constructor.
        // (the OnDirty change broke this and we didn't have a test before)
        [Test]
        public void CurlyBracesConstructor()
        {
            SyncList<int> list = new SyncList<int> { 1, 2, 3 };
            Assert.That(list.Count, Is.EqualTo(3));
        }

        [Test]
        public void TestAdd()
        {
            serverSyncList.Add("yay");
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "World", "!", "yay" }));
        }

        [Test]
        public void TestAddRange()
        {
            serverSyncList.AddRange(new[] { "One", "Two", "Three" });
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList, Is.EqualTo(new[] { "Hello", "World", "!", "One", "Two", "Three" }));
        }

        [Test]
        public void TestClear()
        {
            bool called = false;
            clientSyncList.OnChange = (op, index, oldItem) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_CLEAR));
                Assert.That(clientSyncList.Count, Is.EqualTo(3));
            };
            clientSyncList.Callback = (op, index, oldItem, newItem) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_CLEAR));
                Assert.That(clientSyncList.Count, Is.EqualTo(3));
            };

            bool actionCalled = false;
            clientSyncList.OnClear = () =>
            {
                actionCalled = true;
                Assert.That(clientSyncList.Count, Is.EqualTo(3));
            };

            serverSyncList.Clear();
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList, Is.EquivalentTo(new string[] { }));
            Assert.That(called, Is.True);
            Assert.That(actionCalled, Is.True);
        }

        [Test]
        public void TestInsert()
        {
            bool called = false;
            clientSyncList.OnChange = (op, index, oldItem) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_INSERT));
                Assert.That(index, Is.EqualTo(0));
                Assert.That(clientSyncList[index], Is.EqualTo("yay"));
            };
            clientSyncList.Callback = (op, index, oldItem, newItem) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_INSERT));
                Assert.That(index, Is.EqualTo(0));
                Assert.That(newItem, Is.EqualTo("yay"));
            };

            bool actionCalled = false;
            clientSyncList.OnInsert = (index) =>
            {
                actionCalled = true;
                Assert.That(index, Is.EqualTo(0));
            };

            serverSyncList.Insert(0, "yay");
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "yay", "Hello", "World", "!" }));
            Assert.That(called, Is.True);
            Assert.That(actionCalled, Is.True);
        }

        [Test]
        public void TestInsertRange()
        {
            serverSyncList.InsertRange(1, new[] { "One", "Two", "Three" });
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList, Is.EqualTo(new[] { "Hello", "One", "Two", "Three", "World", "!" }));
        }

        [Test]
        public void TestSet()
        {
            bool called = false;
            clientSyncList.OnChange = (op, index, oldItem) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_SET));
                Assert.That(index, Is.EqualTo(1));
                Assert.That(oldItem, Is.EqualTo("World"));
                Assert.That(clientSyncList[index], Is.EqualTo("yay"));
            };
            clientSyncList.Callback = (op, index, oldItem, newItem) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_SET));
                Assert.That(index, Is.EqualTo(1));
                Assert.That(oldItem, Is.EqualTo("World"));
                Assert.That(newItem, Is.EqualTo("yay"));
            };

            bool actionCalled = false;
            clientSyncList.OnSet = (index, oldItem) =>
            {
                actionCalled = true;
                Assert.That(index, Is.EqualTo(1));
                Assert.That(oldItem, Is.EqualTo("World"));
            };

            serverSyncList[1] = "yay";
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList[1], Is.EqualTo("yay"));
            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "yay", "!" }));
            Assert.That(called, Is.True);
            Assert.That(actionCalled, Is.True);
        }

        [Test]
        public void TestSetNull()
        {
            bool called = false;
            clientSyncList.OnChange = (op, index, oldItem) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_SET));
                Assert.That(index, Is.EqualTo(1));
                Assert.That(oldItem, Is.EqualTo("World"));
                Assert.That(clientSyncList[index], Is.EqualTo(null));
            };
            clientSyncList.Callback = (op, index, oldItem, newItem) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_SET));
                Assert.That(index, Is.EqualTo(1));
                Assert.That(oldItem, Is.EqualTo("World"));
                Assert.That(newItem, Is.EqualTo(null));
            };

            bool actionCalled = false;
            clientSyncList.OnSet = (index, oldItem) =>
            {
                actionCalled = true;
                Assert.That(index, Is.EqualTo(1));
                Assert.That(oldItem, Is.EqualTo("World"));
            };

            serverSyncList[1] = null;
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList[1], Is.EqualTo(null));
            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", null, "!" }));
            Assert.That(called, Is.True);
            Assert.That(actionCalled, Is.True);

            // clear handlers so we don't get called again
            clientSyncList.OnChange = null;
            clientSyncList.Callback = null;
            clientSyncList.OnSet = null;

            serverSyncList[1] = "yay";
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "yay", "!" }));
        }

        [Test]
        public void TestRemoveAll()
        {
            bool called = false;
            clientSyncList.OnChange = (op, index, oldItem) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_REMOVEAT));
                Assert.That(index, Is.EqualTo(0));
                Assert.That(oldItem, Is.Not.EqualTo("!"));
            };
            clientSyncList.Callback = (op, index, oldItem, newItem) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_REMOVEAT));
                Assert.That(index, Is.EqualTo(0));
                Assert.That(oldItem, Is.Not.EqualTo("!"));
            };

            bool actionCalled = false;
            clientSyncList.OnRemove = (index, item) =>
            {
                actionCalled = true;
                Assert.That(index, Is.EqualTo(0));
                Assert.That(item, Is.Not.EqualTo("!"));
            };

            // This will remove "Hello" and "World"
            serverSyncList.RemoveAll(entry => entry.Contains("l"));
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "!" }));
            Assert.That(called, Is.True);
            Assert.That(actionCalled, Is.True);
        }

        [Test]
        public void TestRemoveAllNone()
        {
            serverSyncList.RemoveAll(entry => entry == "yay");
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "World", "!" }));
        }

        [Test]
        public void TestRemoveAt()
        {
            bool called = false;
            clientSyncList.OnChange = (op, index, oldItem) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_REMOVEAT));
                Assert.That(index, Is.EqualTo(1));
                Assert.That(oldItem, Is.EqualTo("World"));
            };
            clientSyncList.Callback = (op, index, oldItem, newItem) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_REMOVEAT));
                Assert.That(index, Is.EqualTo(1));
                Assert.That(oldItem, Is.EqualTo("World"));
            };

            bool actionCalled = false;
            clientSyncList.OnRemove = (index, oldItem) =>
            {
                actionCalled = true;
                Assert.That(index, Is.EqualTo(1));
                Assert.That(oldItem, Is.EqualTo("World"));
            };

            serverSyncList.RemoveAt(1);
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "!" }));
            Assert.That(called, Is.True);
            Assert.That(actionCalled, Is.True);
        }

        [Test]
        public void TestRemove()
        {
            bool called = false;
            clientSyncList.OnChange = (op, index, oldItem) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_REMOVEAT));
                Assert.That(index, Is.EqualTo(1));
                Assert.That(oldItem, Is.EqualTo("World"));
            };
            clientSyncList.Callback = (op, index, oldItem, newItem) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_REMOVEAT));
                Assert.That(index, Is.EqualTo(1));
                Assert.That(oldItem, Is.EqualTo("World"));
            };

            bool actionCalled = false;
            clientSyncList.OnRemove = (index, oldItem) =>
            {
                actionCalled = true;
                Assert.That(index, Is.EqualTo(1));
                Assert.That(oldItem, Is.EqualTo("World"));
            };

            serverSyncList.Remove("World");
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "!" }));
            Assert.That(called, Is.True);
            Assert.That(actionCalled, Is.True);
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
            SyncList<int> serverList = new SyncList<int>();
            SyncList<int> clientList = new SyncList<int>();

            serverList.Add(1);
            serverList.Add(2);
            serverList.Add(3);
            SerializeDeltaTo(serverList, clientList);

            Assert.That(clientList, Is.EquivalentTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void SyncListBoolTest()
        {
            SyncList<bool> serverList = new SyncList<bool>();
            SyncList<bool> clientList = new SyncList<bool>();

            serverList.Add(true);
            serverList.Add(false);
            serverList.Add(true);
            SerializeDeltaTo(serverList, clientList);

            Assert.That(clientList, Is.EquivalentTo(new[] { true, false, true }));
        }

        [Test]
        public void SyncListUIntTest()
        {
            SyncList<uint> serverList = new SyncList<uint>();
            SyncList<uint> clientList = new SyncList<uint>();

            serverList.Add(1U);
            serverList.Add(2U);
            serverList.Add(3U);
            SerializeDeltaTo(serverList, clientList);

            Assert.That(clientList, Is.EquivalentTo(new[] { 1U, 2U, 3U }));
        }

        [Test]
        public void SyncListFloatTest()
        {
            SyncList<float> serverList = new SyncList<float>();
            SyncList<float> clientList = new SyncList<float>();

            serverList.Add(1.0F);
            serverList.Add(2.0F);
            serverList.Add(3.0F);
            SerializeDeltaTo(serverList, clientList);

            Assert.That(clientList, Is.EquivalentTo(new[] { 1.0F, 2.0F, 3.0F }));
        }

        [Test]
        public void CallbackTest()
        {
            bool actionCalled = false;
            clientSyncList.OnAdd = (index) =>
            {
                actionCalled = true;
                Assert.That(index, Is.EqualTo(3));
                Assert.That(clientSyncList[index], Is.EqualTo("yay"));
            };

            bool changeActionCalled = false;
            clientSyncList.OnChange = (op, index, newItem) =>
            {
                changeActionCalled = true;
                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_ADD));
                Assert.That(index, Is.EqualTo(3));
                Assert.That(newItem, Is.EqualTo("yay"));
                Assert.That(clientSyncList[index], Is.EqualTo("yay"));
            };
            bool callbackActionCalled = false;
            clientSyncList.Callback = (op, index, oldItem, newItem) =>
            {
                callbackActionCalled = true;
                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_ADD));
                Assert.That(index, Is.EqualTo(3));
                Assert.That(oldItem, Is.Null);
                Assert.That(newItem, Is.EqualTo("yay"));
            };

            serverSyncList.Add("yay");
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(actionCalled, Is.True);
            Assert.That(changeActionCalled, Is.True);
            Assert.That(callbackActionCalled, Is.True);
        }

        [Test]
        public void CallbackRemoveTest()
        {
            bool actionCalled = false;
            clientSyncList.OnRemove = (index, oldItem) =>
            {
                actionCalled = true;
                Assert.That(index, Is.EqualTo(1));
                Assert.That(oldItem, Is.EqualTo("World"));
            };

            bool changeActionCalled = false;
            clientSyncList.OnChange = (op, index, oldItem) =>
            {
                changeActionCalled = true;
                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_REMOVEAT));
                Assert.That(index, Is.EqualTo(1));
            };
            bool callbackActionCalled = false;
            clientSyncList.Callback = (op, index, oldItem, newItem) =>
            {
                callbackActionCalled = true;
                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_REMOVEAT));
                Assert.That(index, Is.EqualTo(1));
            };

            serverSyncList.Remove("World");
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(actionCalled, Is.True);
            Assert.That(changeActionCalled, Is.True);
            Assert.That(callbackActionCalled, Is.True);
        }

        [Test]
        public void CallbackRemoveAtTest()
        {
            bool actionCalled = false;
            clientSyncList.OnRemove = (index, oldItem) =>
            {
                actionCalled = true;
                Assert.That(index, Is.EqualTo(1));
                Assert.That(oldItem, Is.EqualTo("World"));
            };

            bool changeActionCalled = false;
            clientSyncList.OnChange = (op, index, oldItem) =>
            {
                changeActionCalled = true;
                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_REMOVEAT));
                Assert.That(index, Is.EqualTo(1));
                Assert.That(oldItem, Is.EqualTo("World"));
            };
            bool callbackActionCalled = false;
            clientSyncList.Callback = (op, index, oldItem, newItem) =>
            {
                callbackActionCalled = true;
                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_REMOVEAT));
                Assert.That(index, Is.EqualTo(1));
                Assert.That(oldItem, Is.EqualTo("World"));
                Assert.That(newItem, Is.Null);
            };

            serverSyncList.RemoveAt(1);
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(actionCalled, Is.True);
            Assert.That(changeActionCalled, Is.True);
            Assert.That(callbackActionCalled, Is.True);
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
            Assert.That(clientSyncList.IsReadOnly, Is.True);
        }
        [Test]
        public void WritingToReadOnlyThrows()
        {
            Assert.Throws<InvalidOperationException>(() => { clientSyncList.Add("fail"); });
        }

        [Test]
        public void DirtyTest()
        {
            // Sync Delta to clear dirty
            Assert.That(serverSyncListDirtyCalled, Is.EqualTo(0));
            Assert.That(clientSyncListDirtyCalled, Is.EqualTo(0));
            SerializeDeltaTo(serverSyncList, clientSyncList);

            // nothing to send
            Assert.That(serverSyncListDirtyCalled, Is.EqualTo(0));
            Assert.That(clientSyncListDirtyCalled, Is.EqualTo(0));

            // something has changed
            serverSyncList.Add("1");
            Assert.That(serverSyncListDirtyCalled, Is.EqualTo(1));
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncListDirtyCalled, Is.EqualTo(1));
        }

        [Test]
        public void ObjectCanBeReusedAfterReset()
        {
            serverSyncList.Reset();

            // make old client the host
            SyncList<string> hostList = serverSyncList;
            SyncList<string> clientList2 = new SyncList<string>();

            Assert.That(hostList.IsReadOnly, Is.False);

            // Check Add and Sync without errors
            hostList.Add("hello");
            hostList.Add("world");
            SerializeDeltaTo(hostList, clientList2);
        }

        [Test]
        public void ResetShouldClearChanges()
        {
            serverSyncList.Reset();

            Assert.That(serverSyncList.GetChangeCount(), Is.Zero);
        }

        [Test]
        public void ResetShouldClearItems()
        {
            serverSyncList.Reset();

            Assert.That(serverSyncList, Is.Empty);
        }

        [Test]
        public void IsRecording()
        {
            // shouldn't record changes if IsRecording() returns false
            serverSyncList.ClearChanges();
            serverSyncList.IsRecording = () => false;
            serverSyncList.Add("42");
            Assert.That(serverSyncList.GetChangeCount(), Is.EqualTo(0));
        }
    }

    public static class SyncObjectTestMethods
    {
        public static uint GetChangeCount(this SyncObject syncObject)
        {
            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                syncObject.OnSerializeDelta(writer);

                using (NetworkReaderPooled reader = NetworkReaderPool.Get(writer.ToArraySegment()))
                {
                    return reader.ReadUInt();
                }
            }
        }
    }
}
