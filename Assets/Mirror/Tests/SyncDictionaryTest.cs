using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Mirror.Tests
{
    [TestFixture]
    public class SyncDictionaryTest
    {
        public class SyncDictionaryIntString : SyncDictionary<int, string> {}

        SyncDictionaryIntString serverSyncDictionary;
        SyncDictionaryIntString clientSyncDictionary;

        void SerializeAllTo<T>(T fromList, T toList) where T : SyncObject
        {
            NetworkWriter writer = new NetworkWriter();
            fromList.OnSerializeAll(writer);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            toList.OnDeserializeAll(reader);
        }

        void SerializeDeltaTo<T>(T fromList, T toList) where T : SyncObject
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
            serverSyncDictionary = new SyncDictionaryIntString();
            clientSyncDictionary = new SyncDictionaryIntString();

            // add some data to the list
            serverSyncDictionary.Add(0, "Hello");
            serverSyncDictionary.Add(1, "World");
            serverSyncDictionary.Add(2, "!");
            SerializeAllTo(serverSyncDictionary, clientSyncDictionary);
        }

        [Test]
        public void TestInit()
        {
            Dictionary<int, string> comparer = new Dictionary<int, string>
            {
                [0] = "Hello",
                [1] = "World",
                [2] = "!"
            };
            Assert.That(clientSyncDictionary[0], Is.EqualTo("Hello"));
            Assert.That(clientSyncDictionary, Is.EquivalentTo(comparer));
        }

        [Test]
        public void TestAdd()
        {
            serverSyncDictionary.Add(4, "yay");
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            Assert.That(clientSyncDictionary.ContainsKey(4), Is.EqualTo(true));
            Assert.That(clientSyncDictionary[4], Is.EqualTo("yay"));
        }

        [Test]
        public void TestClear()
        {
            serverSyncDictionary.Clear();
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            Assert.That(serverSyncDictionary, Is.EquivalentTo(new SyncDictionaryIntString()));
        }

        [Test]
        public void TestSet()
        {
            serverSyncDictionary[1] = "yay";
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            Assert.That(clientSyncDictionary.ContainsKey(1));
            Assert.That(clientSyncDictionary[1], Is.EqualTo("yay"));
        }

        [Test]
        public void TestBareSet()
        {
            serverSyncDictionary[4] = "yay";
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            Assert.That(clientSyncDictionary.ContainsKey(4));
            Assert.That(clientSyncDictionary[4], Is.EqualTo("yay"));
        }

        [Test]
        public void TestBareSetNull()
        {
            serverSyncDictionary[4] = null;
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            Assert.That(clientSyncDictionary[4], Is.Null);
            Assert.That(clientSyncDictionary.ContainsKey(4));
        }

        [Test]
        public void TestConsecutiveSet()
        {
            serverSyncDictionary[1] = "yay";
            serverSyncDictionary[1] = "world";
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            Assert.That(clientSyncDictionary[1], Is.EqualTo("world"));
        }

        [Test]
        public void TestNullSet()
        {
            serverSyncDictionary[1] = null;
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            Assert.That(clientSyncDictionary.ContainsKey(1));
            Assert.That(clientSyncDictionary[1], Is.Null);
        }

        [Test]
        public void TestRemove()
        {
            serverSyncDictionary.Remove(1);
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            Assert.That(clientSyncDictionary.ContainsKey(1), Is.False);
        }

        [Test]
        public void TestMultSync()
        {
            serverSyncDictionary.Add(10, "1");
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            // add some delta and see if it applies
            serverSyncDictionary.Add(11, "2");
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            Assert.That(clientSyncDictionary.ContainsKey(10));
            Assert.That(clientSyncDictionary[10], Is.EqualTo("1"));
            Assert.That(clientSyncDictionary.ContainsKey(11));
            Assert.That(clientSyncDictionary[11], Is.EqualTo("2"));
        }

        [Test]
        public void TestContains()
        {
            Assert.That(!clientSyncDictionary.Contains(new KeyValuePair<int, string>(2, "Hello")));
            serverSyncDictionary[2] = "Hello";
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            Assert.That(clientSyncDictionary.Contains(new KeyValuePair<int, string>(2, "Hello")));
        }

        [Test]
        public void CallbackTest()
        {
            bool called = false;
            clientSyncDictionary.Callback += (op, index, item) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncDictionaryIntString.Operation.OP_ADD));
                Assert.That(index, Is.EqualTo(3));
                Assert.That(item, Is.EqualTo("yay"));
            };
            serverSyncDictionary.Add(3, "yay");
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            Assert.That(called, Is.True);
        }

        [Test]
        public void CallbackRemoveTest()
        {
            bool called = false;
            clientSyncDictionary.Callback += (op, key, item) =>
            {
                called = true;
                Assert.That(op, Is.EqualTo(SyncDictionaryIntString.Operation.OP_REMOVE));
                Assert.That(item, Is.EqualTo("World"));
            };
            serverSyncDictionary.Remove(1);
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            Assert.That(called, Is.True);
        }

        [Test]
        public void CountTest()
        {
            Assert.That(serverSyncDictionary.Count, Is.EqualTo(3));
        }

        [Test]
        public void ReadOnlyTest()
        {
            Assert.That(serverSyncDictionary.IsReadOnly, Is.False);
        }

        [Test]
        public void DirtyTest()
        {
            SyncDictionaryIntString serverList = new SyncDictionaryIntString();
            SyncDictionaryIntString clientList = new SyncDictionaryIntString();

            // nothing to send
            Assert.That(serverList.IsDirty, Is.False);

            // something has changed
            serverList.Add(15, "yay");
            Assert.That(serverList.IsDirty, Is.True);
            SerializeDeltaTo(serverList, clientList);

            // data has been flushed,  should go back to clear
            Assert.That(serverList.IsDirty, Is.False);
        }

        [Test]
        public void ReadonlyTest()
        {
            SyncDictionaryIntString serverList = new SyncDictionaryIntString();
            SyncDictionaryIntString clientList = new SyncDictionaryIntString();

            // data has been flushed,  should go back to clear
            Assert.That(clientList.IsReadOnly, Is.False);

            serverList.Add(20, "yay");
            serverList.Add(30, "hello");
            serverList.Add(35, "world");
            SerializeDeltaTo(serverList, clientList);

            // client list should now lock itself,  trying to modify it
            // should produce an InvalidOperationException
            Assert.That(clientList.IsReadOnly, Is.True);
            Assert.Throws<InvalidOperationException>(() => clientList.Add(50, "fail"));
        }
    }
}
