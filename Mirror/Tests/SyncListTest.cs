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
            serverSyncList.Flush();

        }
        [SetUp]
        public void SetUp()
        {
            serverSyncList = InitServerList<SyncListString>();

            clientSyncList = InitClientList<SyncListString>();

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
            var serverList = InitServerList<SyncListInt>();
            var clientList = InitClientList<SyncListInt>();

            serverList.Add(1);
            serverList.Add(2);
            serverList.Add(3);
            SerializeDeltaTo(serverList, clientList);

            Assert.That(clientList, Is.EquivalentTo(new [] {1,2,3}));
        }

        [Test]
        public void SyncListBoolTest()
        {
            var serverList = InitServerList<SyncListBool>();
            var clientList = InitClientList<SyncListBool>();

            serverList.Add(true);
            serverList.Add(false);
            serverList.Add(true);
            SerializeDeltaTo(serverList, clientList);

            Assert.That(clientList, Is.EquivalentTo(new[] { true, false, true }));
        }

        [Test]
        public void SyncListUintTest()
        {
            var serverList = InitServerList<SyncListUInt>();
            var clientList = InitClientList<SyncListUInt>();

            serverList.Add(1U);
            serverList.Add(2U);
            serverList.Add(3U);
            SerializeDeltaTo(serverList, clientList);

            Assert.That(clientList, Is.EquivalentTo(new[] { 1U, 2U, 3U }));
        }

        [Test]
        public void SyncListFloatTest()
        {
            var serverList = InitServerList<SyncListFloat>();
            var clientList = InitClientList<SyncListFloat>();

            serverList.Add(1.0F);
            serverList.Add(2.0F);
            serverList.Add(3.0F);
            SerializeDeltaTo(serverList, clientList);

            Assert.That(clientList, Is.EquivalentTo(new[] { 1.0F, 2.0F, 3.0F }));
        }

        [Test]
        public void CallbackTest()
        {
            var callbackMock = Substitute.For<SyncListString.SyncListChanged>();
            clientSyncList.Callback = callbackMock;

            serverSyncList.Add("yay");
            SerializeDeltaTo(serverSyncList, clientSyncList);

            callbackMock.Received().Invoke(SyncList<string>.Operation.OP_ADD, 0);
        }

    }
}
