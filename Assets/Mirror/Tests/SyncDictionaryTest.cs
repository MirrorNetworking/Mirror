using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Mirror.Tests
{
    [TestFixture]
    public class SyncDictionaryTest
    {
        public class SyncDictionaryIntString : SyncDictionary<int, string>
        {
            protected override string DeserializeItem(NetworkReader reader) => reader.ReadString();
            protected override int DeserializeKey(NetworkReader reader) => reader.ReadInt32();
            protected override void SerializeItem(NetworkWriter writer, string item) => writer.Write(item);
            protected override void SerializeKey(NetworkWriter writer, int item) => writer.Write(item);
        }

        SyncDictionaryIntString serverSyncDictionary;
        SyncDictionaryIntString clientSyncDictionary;

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
    }
}
