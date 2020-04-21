using System;
using System.Collections.Generic;
using NUnit.Framework;
using NSubstitute;

namespace Mirror.Tests
{
    [TestFixture]
    public class SyncListTest
    {
        SyncListString serverSyncList;
        SyncListString clientSyncList;

        public static void SerializeAllTo<T>(T fromList, T toList) where T : ISyncObject
        {
            var writer = new NetworkWriter();
            fromList.OnSerializeAll(writer);
            var reader = new NetworkReader(writer.ToArray());
            toList.OnDeserializeAll(reader);
        }

        public static void SerializeDeltaTo<T>(T fromList, T toList) where T : ISyncObject
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
            var serverList = new SyncListInt();
            var clientList = new SyncListInt();

            serverList.Add(1);
            serverList.Add(2);
            serverList.Add(3);
            SerializeDeltaTo(serverList, clientList);

            Assert.That(clientList, Is.EquivalentTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void SyncListBoolTest()
        {
            var serverList = new SyncListBool();
            var clientList = new SyncListBool();

            serverList.Add(true);
            serverList.Add(false);
            serverList.Add(true);
            SerializeDeltaTo(serverList, clientList);

            Assert.That(clientList, Is.EquivalentTo(new[] { true, false, true }));
        }

        [Test]
        public void SyncListUintTest()
        {
            var serverList = new SyncListUInt();
            var clientList = new SyncListUInt();

            serverList.Add(1U);
            serverList.Add(2U);
            serverList.Add(3U);
            SerializeDeltaTo(serverList, clientList);

            Assert.That(clientList, Is.EquivalentTo(new[] { 1U, 2U, 3U }));
        }

        [Test]
        public void SyncListFloatTest()
        {
            var serverList = new SyncListFloat();
            var clientList = new SyncListFloat();

            serverList.Add(1.0F);
            serverList.Add(2.0F);
            serverList.Add(3.0F);
            SerializeDeltaTo(serverList, clientList);

            Assert.That(clientList, Is.EquivalentTo(new[] { 1.0F, 2.0F, 3.0F }));
        }

        [Test]
        public void AddClientCallbackTest()
        {
            Action<int, string> callback = Substitute.For<Action<int, string>>();
            clientSyncList.OnInsert += callback;
            serverSyncList.Add("yay");
            SerializeDeltaTo(serverSyncList, clientSyncList);
            callback.Received().Invoke(3, "yay");
        }

        [Test]
        public void InsertClientCallbackTest()
        {
            Action<int, string> callback = Substitute.For<Action<int, string>>();
            clientSyncList.OnInsert += callback;
            serverSyncList.Insert(1, "yay");
            SerializeDeltaTo(serverSyncList, clientSyncList);
            callback.Received().Invoke(1, "yay");
        }

        [Test]
        public void RemoveClientCallbackTest()
        {
            Action<int, string> callback = Substitute.For<Action<int, string>>();
            clientSyncList.OnRemove += callback;
            serverSyncList.Remove("World");
            SerializeDeltaTo(serverSyncList, clientSyncList);
            callback.Received().Invoke(1, "World");
        }

        [Test]
        public void ClearClientCallbackTest()
        {
            Action callback = Substitute.For<Action>();
            clientSyncList.OnClear += callback;
            serverSyncList.Clear();
            SerializeDeltaTo(serverSyncList, clientSyncList);
            callback.Received().Invoke();
        }

        [Test]
        public void SetClientCallbackTest()
        {
            Action<int, string, string> callback = Substitute.For<Action<int, string, string>>();
            clientSyncList.OnSet += callback;
            serverSyncList[1] = "yo mama";
            SerializeDeltaTo(serverSyncList, clientSyncList);
            callback.Received().Invoke(1, "World", "yo mama");
        }

        [Test]
        public void ChangeClientCallbackTest()
        {
            Action callback = Substitute.For<Action>();
            clientSyncList.OnChange += callback;
            serverSyncList.Add("1");
            serverSyncList.Add("2");
            SerializeDeltaTo(serverSyncList, clientSyncList);
            callback.Received(1).Invoke();
        }

        [Test]
        public void AddServerCallbackTest()
        {
            Action<int, string> callback = Substitute.For<Action<int, string>>();
            serverSyncList.OnInsert += callback;
            serverSyncList.Add("yay");
            callback.Received().Invoke(3, "yay");
        }

        [Test]
        public void InsertServerCallbackTest()
        {
            Action<int, string> callback = Substitute.For<Action<int, string>>();
            serverSyncList.OnInsert += callback;
            serverSyncList.Insert(1, "yay");
            callback.Received().Invoke(1, "yay");
        }

        [Test]
        public void RemoveServerCallbackTest()
        {
            Action<int, string> callback = Substitute.For<Action<int, string>>();
            serverSyncList.OnRemove += callback;
            serverSyncList.Remove("World");
            callback.Received().Invoke(1, "World");
        }

        [Test]
        public void ClearServerCallbackTest()
        {
            Action callback = Substitute.For<Action>();
            serverSyncList.OnClear += callback;
            serverSyncList.Clear();
            callback.Received().Invoke();
        }

        [Test]
        public void SetServerCallbackTest()
        {
            Action<int, string, string> callback = Substitute.For<Action<int, string, string>>();
            serverSyncList.OnSet += callback;
            serverSyncList[1] = "yo mama";
            callback.Received().Invoke(1, "World", "yo mama");
        }

        [Test]
        public void ChangeServerCallbackTest()
        {
            Action callback = Substitute.For<Action>();
            serverSyncList.OnChange += callback;
            serverSyncList.Add("1");
            serverSyncList.Add("2");
            // note that on the server we would receive 2 calls
            // because we are adding 2 operations separately
            // there is no way to batch operations in the server
            callback.Received().Invoke();
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
            SerializeDeltaTo(serverSyncList, clientSyncList);

            // nothing to send
            Assert.That(serverSyncList.IsDirty, Is.False);

            // something has changed
            serverSyncList.Add("1");
            Assert.That(serverSyncList.IsDirty, Is.True);
            SerializeDeltaTo(serverSyncList, clientSyncList);

            // data has been flushed,  should go back to clear
            Assert.That(serverSyncList.IsDirty, Is.False);
        }

        [Test]
        public void ObjectCanBeReusedAfterReset()
        {
            clientSyncList.Reset();

            // make old client the host
            SyncListString hostList = clientSyncList;
            SyncListString clientList2 = new SyncListString();

            Assert.That(hostList.IsReadOnly, Is.False);

            // Check Add and Sync without errors
            hostList.Add("hello");
            hostList.Add("world");
            SerializeDeltaTo(hostList, clientList2);
        }

        [Test]
        public void ResetShouldSetReadOnlyToFalse()
        {
            clientSyncList.Reset();

            Assert.That(clientSyncList.IsReadOnly, Is.False);
        }

        [Test]
        public void ResetShouldClearChanges()
        {
            serverSyncList.Reset();

            Assert.That(serverSyncList.ChangeCount, Is.Zero);
        }

        [Test]
        public void ResetShouldClearItems()
        {
            serverSyncList.Reset();

            Assert.That(serverSyncList, Is.Empty);
        }
    }
}
