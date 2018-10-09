using NUnit.Framework;
using System;
using NSubstitute;

namespace Mirror.Tests
{
    [TestFixture]
    public class SyncListTest
    {
        SyncListString serverSyncList;
        SyncListString clientSyncList;


        [SetUp]
        public void SetUp()
        {
            serverSyncList = new SyncListString();
            INetworkBehaviour serverBehavior = Substitute.For<INetworkBehaviour>();
            serverBehavior.isServer.Returns(true);
            serverBehavior.isClient.Returns(false);
            serverSyncList.InitializeBehaviour(serverBehavior);

            clientSyncList = new SyncListString();
            INetworkBehaviour clientBehavior = Substitute.For<INetworkBehaviour>();
            clientBehavior.isServer.Returns(true);
            clientBehavior.isClient.Returns(false);
            clientSyncList.InitializeBehaviour(clientBehavior);
        }


        [Test]
        public void TestInit()
        {
            NetworkWriter writer = new NetworkWriter();

            serverSyncList.Add("Hello");
            serverSyncList.Add("World");
            serverSyncList.Add("!");

            serverSyncList.OnSerializeAll(writer);

            NetworkReader reader = new NetworkReader(writer.ToArray());

            clientSyncList.OnDeserializeAll(reader);

            Assert.That(clientSyncList, Is.EquivalentTo(new []{"Hello", "World", "!"}));
        }

        [Test]
        public void TestAdd()
        {
            NetworkWriter writer = new NetworkWriter();
            serverSyncList.Add("Hello");
            serverSyncList.OnSerializeAll(writer);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeAll(reader);

            // list has been initialized

            // add some delta and see if it applies
            writer = new NetworkWriter();
            serverSyncList.Add("World");
            serverSyncList.OnSerializeDelta(writer);
            reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeDelta(reader);

            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "World" }));

        }

        [Test]
        public void TestClear()
        {
            NetworkWriter writer = new NetworkWriter();
            serverSyncList.Add("Hello");
            serverSyncList.OnSerializeAll(writer);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeAll(reader);

            // list has been initialized

            // add some delta and see if it applies
            writer = new NetworkWriter();
            serverSyncList.Clear();
            serverSyncList.OnSerializeDelta(writer);
            reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeDelta(reader);

            Assert.That(clientSyncList, Is.EquivalentTo(new string[] { }));

        }

        [Test]
        public void TestInsert()
        {
            NetworkWriter writer = new NetworkWriter();
            serverSyncList.Add("World");
            serverSyncList.OnSerializeAll(writer);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeAll(reader);

            // list has been initialized

            // add some delta and see if it applies
            writer = new NetworkWriter();
            serverSyncList.Insert(0,"Hello");
            serverSyncList.OnSerializeDelta(writer);
            reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeDelta(reader);

            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "World" }));

        }

        [Test]
        public void TestSet()
        {
            NetworkWriter writer = new NetworkWriter();
            serverSyncList.Add("Hello");
            serverSyncList.Add("Joe");
            serverSyncList.Add("!");
            serverSyncList.OnSerializeAll(writer);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeAll(reader);

            // list has been initialized

            // add some delta and see if it applies
            writer = new NetworkWriter();
            serverSyncList[1] = "World";
            serverSyncList.OnSerializeDelta(writer);
            reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeDelta(reader);

            Assert.That(clientSyncList[1], Is.EqualTo("World"));
            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "World", "!" }));

        }

        [Test]
        public void TestRemoveAt()
        {
            NetworkWriter writer = new NetworkWriter();
            serverSyncList.Add("Hello");
            serverSyncList.Add("Joe");
            serverSyncList.Add("World");
            serverSyncList.OnSerializeAll(writer);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeAll(reader);

            // list has been initialized

            // add some delta and see if it applies
            writer = new NetworkWriter();
            serverSyncList.RemoveAt(1);
            serverSyncList.OnSerializeDelta(writer);
            reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeDelta(reader);

            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "World" }));

        }

        [Test]
        public void TestRemove()
        {
            NetworkWriter writer = new NetworkWriter();
            serverSyncList.Add("Hello");
            serverSyncList.Add("Joe");
            serverSyncList.Add("World");
            serverSyncList.OnSerializeAll(writer);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeAll(reader);

            // list has been initialized

            // add some delta and see if it applies
            writer = new NetworkWriter();
            serverSyncList.Remove("Joe");
            serverSyncList.OnSerializeDelta(writer);
            reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeDelta(reader);

            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "World" }));

        }


        [Test]
        public void TestMultSync()
        {
            NetworkWriter writer = new NetworkWriter();
            serverSyncList.Add("Hello");
            serverSyncList.OnSerializeAll(writer);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeAll(reader);

            // list has been initialized

            // add some delta and see if it applies
            writer = new NetworkWriter();
            serverSyncList.Add("World");
            serverSyncList.OnSerializeDelta(writer);
            reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeDelta(reader);

            // after a non init,  we should flush
            serverSyncList.Flush();

            // add some delta and see if it applies
            writer = new NetworkWriter();
            serverSyncList.Add("!");
            serverSyncList.OnSerializeDelta(writer);
            reader = new NetworkReader(writer.ToArray());
            clientSyncList.OnDeserializeDelta(reader);

            // after a non init,  we should flush
            serverSyncList.Flush();

            Assert.That(clientSyncList, Is.EquivalentTo(new[] { "Hello", "World", "!" }));

        }
    }
}
