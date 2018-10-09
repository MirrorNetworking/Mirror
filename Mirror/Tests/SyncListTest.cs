using NUnit.Framework;
using System;
using NSubstitute;
using System.Linq;


namespace Mirror.Tests
{
    [TestFixture]
    public class SyncListTest
    {
        SyncListString serverSyncList;
        SyncListString clientSyncList;

        private T InitServerList<T>() where T : SyncObject, new() {
            T list = new T();
            INetworkBehaviour serverBehavior = Substitute.For<INetworkBehaviour>();
            serverBehavior.isServer.Returns(true);
            serverBehavior.isClient.Returns(false);
            list.InitializeBehaviour(serverBehavior);
            return list;
        }

        private T InitClientList<T>() where T : SyncObject, new()
        {
            T list = new T();
            INetworkBehaviour clientBehavior = Substitute.For<INetworkBehaviour>();
            clientBehavior.isServer.Returns(false);
            clientBehavior.isClient.Returns(true);
            list.InitializeBehaviour(clientBehavior);
            return list;
        }

        [SetUp]
        public void SetUp()
        {
            serverSyncList = InitServerList<SyncListString>();

            clientSyncList = InitClientList<SyncListString>();

            // add some data to the list
            NetworkWriter writer = new NetworkWriter();
            serverSyncList.Add("Hello");
            serverSyncList.Add("World");
            serverSyncList.Add("!");
            serverSyncList.OnSerializeAll(writer);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeAll(reader);

        }


        [Test]
        public void TestInit()
        {
            Assert.That(clientSyncList, Is.EquivalentTo(new []{"Hello", "World", "!"}));
        }

        [Test]
        public void TestAdd()
        {
            // add some delta and see if it applies
            var writer  = new NetworkWriter();
            serverSyncList.Add("yay");
            serverSyncList.OnSerializeDelta(writer);
            var reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeDelta(reader);

            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "World", "!", "yay" }));
        }

        [Test]
        public void TestClear()
        {
            // add some delta and see if it applies
            var writer = new NetworkWriter();
            serverSyncList.Clear();
            serverSyncList.OnSerializeDelta(writer);
            var reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeDelta(reader);

            Assert.That(clientSyncList, Is.EquivalentTo(new string[] { }));

        }

        [Test]
        public void TestInsert()
        {
            // add some delta and see if it applies
            var writer = new NetworkWriter();
            serverSyncList.Insert(0,"yay");
            serverSyncList.OnSerializeDelta(writer);
            var reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeDelta(reader);

            Assert.That(clientSyncList, Is.EquivalentTo(new[] {"yay", "Hello", "World", "!" }));

        }

        [Test]
        public void TestSet()
        {
            // add some delta and see if it applies
            var writer = new NetworkWriter();
            serverSyncList[1] = "yay";
            serverSyncList.OnSerializeDelta(writer);
            var reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeDelta(reader);

            Assert.That(clientSyncList[1], Is.EqualTo("yay"));
            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "yay", "!" }));

        }

        [Test]
        public void TestRemoveAt()
        {
            // add some delta and see if it applies
            var writer = new NetworkWriter();
            serverSyncList.RemoveAt(1);
            serverSyncList.OnSerializeDelta(writer);
            var reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeDelta(reader);

            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "!" }));

        }

        [Test]
        public void TestRemove()
        {
            // add some delta and see if it applies
            var writer = new NetworkWriter();
            serverSyncList.Remove("World");
            serverSyncList.OnSerializeDelta(writer);
            var reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeDelta(reader);

            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "!" }));

        }


        [Test]
        public void TestMultSync()
        {
            // add some delta and see if it applies
            var writer = new NetworkWriter();
            serverSyncList.Add("1");
            serverSyncList.OnSerializeDelta(writer);
            var reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeDelta(reader);

            // after a non init,  we should flush
            serverSyncList.Flush();

            // add some delta and see if it applies
            writer = new NetworkWriter();
            serverSyncList.Add("2");
            serverSyncList.OnSerializeDelta(writer);
            reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeDelta(reader);

            // after a non init,  we should flush
            serverSyncList.Flush();

            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "World", "!", "1","2" }));

        }


        [Test]
        public void SyncListIntTest()
        {
            var serverList = InitServerList<SyncListInt>();

            var clientList = InitClientList<SyncListInt>();

            serverList.Add(1);
            serverList.Add(2);
            serverList.Add(3);

            // add some data to the list
            NetworkWriter writer = new NetworkWriter();
            serverList.OnSerializeAll(writer);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            clientList.OnDeserializeAll(reader);

            Assert.That(clientList, Is.EquivalentTo(new [] {1,2,3}));
        }
    }
}
