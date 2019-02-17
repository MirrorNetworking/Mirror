﻿using NUnit.Framework;
using System;
using System.Linq;

namespace Mirror.Tests
{
    [TestFixture]
    public class SyncListTest
    {
        SyncListString serverSyncList;
        SyncListString clientSyncList;

        private void SerializeAllTo<T>(T fromList, T toList) where T: SyncObject
        {
            NetworkWriter writer = new NetworkWriter();
            fromList.OnSerializeAll(writer);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            toList.OnDeserializeAll(reader);
        }

        private void SerializeDeltaTo<T>(T fromList, T toList) where T : SyncObject
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
            Assert.That(clientSyncList, Is.EquivalentTo(new []{"Hello", "World", "!"}));
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
            serverSyncList.Insert(0,"yay");
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList, Is.EquivalentTo(new[] {"yay", "Hello", "World", "!" }));
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
        public void TestMultSync()
        {
            serverSyncList.Add("1");
            SerializeDeltaTo(serverSyncList, clientSyncList);
            // add some delta and see if it applies
            serverSyncList.Add("2");
            SerializeDeltaTo(serverSyncList, clientSyncList);
            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "World", "!", "1","2" }));
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

            Assert.That(clientList, Is.EquivalentTo(new [] {1,2,3}));
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

            clientSyncList.Callback += (op, index, item) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_ADD));
                Assert.That(index, Is.EqualTo(3));
                Assert.That(item, Is.EqualTo("yay"));
            };

            serverSyncList.Add("yay");
            SerializeDeltaTo(serverSyncList, clientSyncList);


            Assert.That(called, Is.True);
        }

        [Test]
        public void CallbackRemoveTest()
        {
            bool called = false;

            clientSyncList.Callback += (op, index, item) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_REMOVE));
                Assert.That(item, Is.EqualTo("World"));
            };
            serverSyncList.Remove("World");
            SerializeDeltaTo(serverSyncList, clientSyncList);

            Assert.That(called, Is.True);
        }

        [Test]
        public void CallbackRemoveAtTest()
        {
            bool called = false;

            clientSyncList.Callback += (op, index, item) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncList<string>.Operation.OP_REMOVEAT));
                Assert.That(index, Is.EqualTo(1));
                Assert.That(item, Is.EqualTo("World"));
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
}
