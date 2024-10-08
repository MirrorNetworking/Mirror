using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Mirror.Tests.SyncCollections
{
    [TestFixture]
    public class SyncDictionaryTest
    {
        SyncDictionary<int, string> serverSyncDictionary;
        SyncDictionary<int, string> clientSyncDictionary;
        int serverSyncDictionaryDirtyCalled;
        int clientSyncDictionaryDirtyCalled;

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
            fromList.ClearChanges();
        }

        [SetUp]
        public void SetUp()
        {
            serverSyncDictionary = new SyncDictionary<int, string>();
            clientSyncDictionary = new SyncDictionary<int, string>();

            // set writable
            serverSyncDictionary.IsWritable = () => true;
            clientSyncDictionary.IsWritable = () => false;

            // add some data to the list
            serverSyncDictionary.Add(0, "Hello");
            serverSyncDictionary.Add(1, "World");
            serverSyncDictionary.Add(2, "!");
            SerializeAllTo(serverSyncDictionary, clientSyncDictionary);

            // set up dirty callbacks for testing.
            // AFTER adding the example data. we already know we added that data.
            serverSyncDictionaryDirtyCalled = 0;
            clientSyncDictionaryDirtyCalled = 0;
            serverSyncDictionary.OnDirty = () => ++serverSyncDictionaryDirtyCalled;
            clientSyncDictionary.OnDirty = () => ++clientSyncDictionaryDirtyCalled;
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

        // test the '= List<int>{1,2,3}' constructor.
        // it calls .Add(1); .Add(2); .Add(3) in the constructor.
        // (the OnDirty change broke this and we didn't have a test before)
        [Test]
        public void CurlyBracesConstructor()
        {
            SyncDictionary<int,string> dict = new SyncDictionary<int, string>{{1,"1"}, {2,"2"}, {3,"3"}};
            Assert.That(dict.Count, Is.EqualTo(3));
        }

        [Test]
        public void TestAdd()
        {
            // Adds a new entry with index of 4 using .Add method
            bool actionCalled = false;
            clientSyncDictionary.OnAdd = (key) =>
            {
                actionCalled = true;
                Assert.That(key, Is.EqualTo(4));
                Assert.That(clientSyncDictionary[key], Is.EqualTo("yay"));
            };

            serverSyncDictionary.Add(4, "yay");
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            Assert.That(clientSyncDictionary.ContainsKey(4));
            Assert.That(clientSyncDictionary[4], Is.EqualTo("yay"));
            Assert.That(actionCalled, Is.True);
        }

        [Test]
        public void TestClear()
        {
            // Verifies that the clear method works and that the data is still present for the Callback.
            bool actionCalled = false;
            clientSyncDictionary.OnClear = () =>
            {
                actionCalled = true;
                Assert.That(clientSyncDictionary.Count, Is.EqualTo(3));
            };

            serverSyncDictionary.Clear();
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            Assert.That(serverSyncDictionary, Is.EquivalentTo(new SyncDictionary<int, string>()));
            Assert.That(actionCalled, Is.True);
        }

        [Test]
        public void TestSet()
        {
            // Overwrites an existing entry
            bool actionCalled = false;
            clientSyncDictionary.OnSet = (key, oldItem) =>
            {
                actionCalled = true;
                Assert.That(key, Is.EqualTo(1));
                Assert.That(oldItem, Is.EqualTo("World"));
                Assert.That(clientSyncDictionary[key], Is.EqualTo("yay"));
            };

            serverSyncDictionary[1] = "yay";
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            Assert.That(clientSyncDictionary.ContainsKey(1));
            Assert.That(clientSyncDictionary[1], Is.EqualTo("yay"));
            Assert.That(actionCalled, Is.True);
        }

        [Test]
        public void TestBareSet()
        {
            // Adds a new entry with index of 4 without using .Add method
            bool actionCalled = false;
            clientSyncDictionary.OnAdd = (key) =>
            {
                actionCalled = true;
                Assert.That(key, Is.EqualTo(4));
                Assert.That(clientSyncDictionary[key], Is.EqualTo("yay"));
            };

            serverSyncDictionary[4] = "yay";
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            Assert.That(clientSyncDictionary.ContainsKey(4));
            Assert.That(clientSyncDictionary[4], Is.EqualTo("yay"));
            Assert.That(actionCalled, Is.True);
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
            Assert.That(!clientSyncDictionary.ContainsKey(1));
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
            bool actionCalled = false;
            clientSyncDictionary.OnAdd = (key) =>
            {
                actionCalled = true;
                Assert.That(key, Is.EqualTo(3));
                Assert.That(clientSyncDictionary[key], Is.EqualTo("yay"));
            };

            bool changeActionCalled = false;
            clientSyncDictionary.OnChange = (op, key, item) =>
            {
                changeActionCalled = true;
                Assert.That(op, Is.EqualTo(SyncDictionary<int, string>.Operation.OP_ADD));
                Assert.That(key, Is.EqualTo(3));
                Assert.That(clientSyncDictionary[key], Is.EqualTo("yay"));
            };

            serverSyncDictionary.Add(3, "yay");
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            Assert.That(actionCalled, Is.True);
            Assert.That(changeActionCalled, Is.True);
        }

        [Test]
        public void ServerCallbackTest()
        {
            bool actionCalled = false;
            serverSyncDictionary.OnAdd = (key) =>
            {
                actionCalled = true;
                Assert.That(key, Is.EqualTo(3));
                Assert.That(serverSyncDictionary[key], Is.EqualTo("yay"));
            };

            bool changeActionCalled = false;
            serverSyncDictionary.OnChange = (op, key, item) =>
            {
                changeActionCalled = true;
                Assert.That(op, Is.EqualTo(SyncDictionary<int, string>.Operation.OP_ADD));
                Assert.That(key, Is.EqualTo(3));
                Assert.That(serverSyncDictionary[key], Is.EqualTo("yay"));
            };

            serverSyncDictionary[3] = "yay";
            Assert.That(actionCalled, Is.True);
            Assert.That(changeActionCalled, Is.True);
        }

        [Test]
        public void CallbackRemoveTest()
        {
            bool actionCalled = false;
            clientSyncDictionary.OnRemove = (key, oldItem) =>
            {
                actionCalled = true;
                Assert.That(key, Is.EqualTo(1));
                Assert.That(oldItem, Is.EqualTo("World"));
                Assert.That(!clientSyncDictionary.ContainsKey(1));
            };

            bool changeActionCalled = false;
            clientSyncDictionary.OnChange = (op, key, item) =>
            {
                changeActionCalled = true;
                Assert.That(op, Is.EqualTo(SyncDictionary<int, string>.Operation.OP_REMOVE));
                Assert.That(key, Is.EqualTo(1));
                Assert.That(item, Is.EqualTo("World"));
            };

            serverSyncDictionary.Remove(1);
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            Assert.That(actionCalled, Is.True);
            Assert.That(changeActionCalled, Is.True);
        }

        [Test]
        public void CountTest()
        {
            Assert.That(serverSyncDictionary.Count, Is.EqualTo(3));
        }

        [Test]
        public void CopyToTest()
        {
            KeyValuePair<int, string>[] data = new KeyValuePair<int, string>[3];

            clientSyncDictionary.CopyTo(data, 0);

            Assert.That(data, Is.EquivalentTo(new KeyValuePair<int, string>[]
            {
                new KeyValuePair<int, string>(0, "Hello"),
                new KeyValuePair<int, string>(1, "World"),
                new KeyValuePair<int, string>(2, "!"),

            }));
        }

        [Test]
        public void CopyToOutOfRangeTest()
        {
            KeyValuePair<int, string>[] data = new KeyValuePair<int, string>[3];

            Assert.Throws(typeof(ArgumentOutOfRangeException), delegate
            {
                clientSyncDictionary.CopyTo(data, -1);
            });
        }

        [Test]
        public void CopyToOutOfBoundsTest()
        {
            KeyValuePair<int, string>[] data = new KeyValuePair<int, string>[3];

            Assert.Throws(typeof(ArgumentException), delegate
            {
                clientSyncDictionary.CopyTo(data, 2);
            });
        }

        [Test]
        public void TestRemovePair()
        {
            KeyValuePair<int, string> data = new KeyValuePair<int, string>(0, "Hello");

            serverSyncDictionary.Remove(data);

            Assert.That(serverSyncDictionary, Is.EquivalentTo(new KeyValuePair<int, string>[]
            {
                new KeyValuePair<int, string>(1, "World"),
                new KeyValuePair<int, string>(2, "!"),
            }));
        }

        [Test]
        public void ReadOnlyTest()
        {
            Assert.That(serverSyncDictionary.IsReadOnly, Is.False);
            Assert.That(clientSyncDictionary.IsReadOnly, Is.True);
        }

        [Test]
        public void WritingToReadOnlyThrows()
        {
            Assert.Throws<InvalidOperationException>(() => clientSyncDictionary.Add(50, "fail"));
        }

        [Test]
        public void DirtyTest()
        {
            // Sync Delta to clear dirty
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);

            // nothing to send
            Assert.That(serverSyncDictionaryDirtyCalled, Is.EqualTo(0));
            Assert.That(clientSyncDictionaryDirtyCalled, Is.EqualTo(0));

            // something has changed
            serverSyncDictionary.Add(15, "yay");
            Assert.That(serverSyncDictionaryDirtyCalled, Is.EqualTo(1));
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            Assert.That(clientSyncDictionaryDirtyCalled, Is.EqualTo(1));
        }

        [Test]
        public void ObjectCanBeReusedAfterReset()
        {
            serverSyncDictionary.Reset();

            // make old server the host
            SyncDictionary<int, string> hostList = serverSyncDictionary;
            SyncDictionary<int, string> clientList2 = new SyncDictionary<int, string>();

            // Check Add and Sync without errors
            hostList.Add(30, "hello");
            hostList.Add(35, "world");
            SerializeDeltaTo(hostList, clientList2);
        }

        [Test]
        public void ResetShouldClearChanges()
        {
            serverSyncDictionary.Reset();
            Assert.That(serverSyncDictionary.GetChangeCount(), Is.Zero);
        }

        [Test]
        public void ResetShouldClearItems()
        {
            serverSyncDictionary.Reset();
            Assert.That(serverSyncDictionary, Is.Empty);
        }

        [Test]
        public void IsRecording()
        {
            // shouldn't record changes if IsRecording() returns false
            serverSyncDictionary.ClearChanges();
            serverSyncDictionary.IsRecording = () => false;
            serverSyncDictionary[42] = null;
            Assert.That(serverSyncDictionary.GetChangeCount(), Is.EqualTo(0));
        }
    }
}
