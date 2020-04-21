using System;
using System.Collections.Generic;
using NSubstitute;
using NUnit.Framework;

namespace Mirror.Tests
{
    [TestFixture]
    public class SyncDictionaryTest
    {
        public class SyncDictionaryIntString : SyncDictionary<int, string>
        {
        }

        SyncDictionaryIntString serverSyncDictionary;
        SyncDictionaryIntString clientSyncDictionary;

        void SerializeAllTo<T>(T fromList, T toList) where T : ISyncObject
        {
            var writer = new NetworkWriter();
            fromList.OnSerializeAll(writer);
            var reader = new NetworkReader(writer.ToArray());
            toList.OnDeserializeAll(reader);
        }

        void SerializeDeltaTo<T>(T fromList, T toList) where T : ISyncObject
        {
            var writer = new NetworkWriter();
            fromList.OnSerializeDelta(writer);
            var reader = new NetworkReader(writer.ToArray());
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
            var comparer = new Dictionary<int, string>
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
            Assert.That(clientSyncDictionary.ContainsKey(4));
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
        public void AddClientCallbackTest()
        {
            Action<int, string> callback = Substitute.For<Action<int, string>>();
            clientSyncDictionary.OnInsert += callback;
            serverSyncDictionary.Add(3, "yay");
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            callback.Received().Invoke(3, "yay");
        }

        [Test]
        public void AddServerCallbackTest()
        {
            Action<int, string> callback = Substitute.For<Action<int, string>>();
            serverSyncDictionary.OnInsert += callback;
            serverSyncDictionary.Add(3, "yay");
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            callback.Received().Invoke(3, "yay");
        }

        [Test]
        public void RemoveClientCallbackTest()
        {
            Action<int, string> callback = Substitute.For<Action<int, string>>();
            clientSyncDictionary.OnRemove += callback;
            serverSyncDictionary.Remove(1);
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            callback.Received().Invoke(1, "World");
        }

        [Test]
        public void ClearClientCallbackTest()
        {
            Action callback = Substitute.For<Action>();
            clientSyncDictionary.OnClear += callback;
            serverSyncDictionary.Clear();
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            callback.Received().Invoke();
        }

        [Test]
        public void ChangeClientCallbackTest()
        {
            Action callback = Substitute.For<Action>();
            clientSyncDictionary.OnChange += callback;
            serverSyncDictionary.Add(3, "1");
            serverSyncDictionary.Add(4, "1");
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            callback.Received(1).Invoke();
        }

        [Test]
        public void SetClientCallbackTest()
        {
            Action<int, string, string> callback = Substitute.For<Action<int, string, string>>();
            clientSyncDictionary.OnSet += callback;
            serverSyncDictionary[0] = "yay";
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);
            callback.Received().Invoke(0, "Hello", "yay");
        }

        [Test]
        public void CountTest()
        {
            Assert.That(serverSyncDictionary.Count, Is.EqualTo(3));
        }

        [Test]
        public void CopyToTest()
        {
            var data = new KeyValuePair<int, string>[3];

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
            var data = new KeyValuePair<int, string>[3];

            Assert.Throws(typeof(ArgumentOutOfRangeException), delegate
            {
                clientSyncDictionary.CopyTo(data, -1);
            });
        }

        [Test]
        public void CopyToOutOfBoundsTest()
        {
            var data = new KeyValuePair<int, string>[3];

            Assert.Throws(typeof(ArgumentException), delegate
            {
                clientSyncDictionary.CopyTo(data, 2);
            });
        }

        [Test]
        public void TestRemovePair()
        {
            var data = new KeyValuePair<int, string>(0, "Hello");

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
            Assert.That(serverSyncDictionary.IsDirty, Is.False);

            // something has changed
            serverSyncDictionary.Add(15, "yay");
            Assert.That(serverSyncDictionary.IsDirty, Is.True);
            SerializeDeltaTo(serverSyncDictionary, clientSyncDictionary);

            // data has been flushed,  should go back to clear
            Assert.That(serverSyncDictionary.IsDirty, Is.False);
        }


        [Test]
        public void ObjectCanBeReusedAfterReset()
        {
            clientSyncDictionary.Reset();

            // make old client the host
            SyncDictionaryIntString hostList = clientSyncDictionary;
            SyncDictionaryIntString clientList2 = new SyncDictionaryIntString();

            Assert.That(hostList.IsReadOnly, Is.False);

            // Check Add and Sync without errors
            hostList.Add(30, "hello");
            hostList.Add(35, "world");
            SerializeDeltaTo(hostList, clientList2);
        }

        [Test]
        public void ResetShouldSetReadOnlyToFalse()
        {
            clientSyncDictionary.Reset();

            Assert.That(clientSyncDictionary.IsReadOnly, Is.False);
        }

        [Test]
        public void ResetShouldClearChanges()
        {
            serverSyncDictionary.Reset();

            Assert.That(serverSyncDictionary.ChangeCount, Is.Zero);
        }

        [Test]
        public void ResetShouldClearItems()
        {
            serverSyncDictionary.Reset();

            Assert.That(serverSyncDictionary, Is.Empty);
        }
    }
}
