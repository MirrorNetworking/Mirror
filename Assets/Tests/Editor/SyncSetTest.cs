using System;
using System.Collections.Generic;
using NSubstitute;
using NUnit.Framework;

namespace Mirror.Tests
{
    [TestFixture]
    public class SyncSetTest
    {
        public class SyncSetString : SyncHashSet<string> { }

        SyncSetString serverSyncSet;
        SyncSetString clientSyncSet;

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
            serverSyncSet = new SyncSetString();
            clientSyncSet = new SyncSetString();

            // add some data to the list
            serverSyncSet.Add("Hello");
            serverSyncSet.Add("World");
            serverSyncSet.Add("!");
            SerializeAllTo(serverSyncSet, clientSyncSet);
        }

        [Test]
        public void TestInit()
        {
            Assert.That(serverSyncSet, Is.EquivalentTo(new[] { "Hello", "World", "!" }));
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "Hello", "World", "!" }));
        }

        [Test]
        public void TestAdd()
        {
            serverSyncSet.Add("yay");
            Assert.That(serverSyncSet.IsDirty, Is.True);
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "Hello", "World", "!", "yay" }));
            Assert.That(serverSyncSet.IsDirty, Is.False);
        }

        [Test]
        public void TestClear()
        {
            serverSyncSet.Clear();
            Assert.That(serverSyncSet.IsDirty, Is.True);
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new string[] { }));
            Assert.That(serverSyncSet.IsDirty, Is.False);
        }

        [Test]
        public void TestRemove()
        {
            serverSyncSet.Remove("World");
            Assert.That(serverSyncSet.IsDirty, Is.True);
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "Hello", "!" }));
            Assert.That(serverSyncSet.IsDirty, Is.False);
        }

        [Test]
        public void TestMultSync()
        {
            serverSyncSet.Add("1");
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            // add some delta and see if it applies
            serverSyncSet.Add("2");
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "Hello", "World", "!", "1", "2" }));
        }

        [Test]
        public void AddClientCallbackTest()
        {
            Action<string> callback = Substitute.For<Action<string>>();
            clientSyncSet.OnAdd += callback;
            serverSyncSet.Add("yay");
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            callback.Received().Invoke("yay");
        }

        [Test]
        public void RemoveClientCallbackTest()
        {
            Action<string> callback = Substitute.For<Action<string>>();
            clientSyncSet.OnRemove += callback;
            serverSyncSet.Remove("World");
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            callback.Received().Invoke("World");
        }

        [Test]
        public void ClearClientCallbackTest()
        {
            Action callback = Substitute.For<Action>();
            clientSyncSet.OnClear += callback;
            serverSyncSet.Clear();
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            callback.Received().Invoke();
        }

        [Test]
        public void ChangeClientCallbackTest()
        {
            Action callback = Substitute.For<Action>();
            clientSyncSet.OnChange += callback;
            serverSyncSet.Add("1");
            serverSyncSet.Add("2");
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            callback.Received(1).Invoke();
        }

        [Test]
        public void CountTest()
        {
            Assert.That(serverSyncSet.Count, Is.EqualTo(3));
        }

        [Test]
        public void TestExceptWith()
        {
            serverSyncSet.ExceptWith(new[] { "World", "Hello" });
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "!" }));
        }

        [Test]
        public void TestExceptWithSelf()
        {
            serverSyncSet.ExceptWith(serverSyncSet);
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new String[] { }));
        }

        [Test]
        public void TestIntersectWith()
        {
            serverSyncSet.IntersectWith(new[] { "World", "Hello" });
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "World", "Hello" }));
        }

        [Test]
        public void TestIntersectWithSet()
        {
            serverSyncSet.IntersectWith(new HashSet<string> { "World", "Hello" });
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "World", "Hello" }));
        }

        [Test]
        public void TestIsProperSubsetOf()
        {
            Assert.That(clientSyncSet.IsProperSubsetOf(new[] { "World", "Hello", "!", "pepe" }));
        }

        [Test]
        public void TestIsProperSubsetOfSet()
        {
            Assert.That(clientSyncSet.IsProperSubsetOf(new HashSet<string> { "World", "Hello", "!", "pepe" }));
        }

        [Test]
        public void TestIsNotProperSubsetOf()
        {
            Assert.That(clientSyncSet.IsProperSubsetOf(new[] { "World", "!", "pepe" }), Is.False);
        }

        [Test]
        public void TestIsProperSuperSetOf()
        {
            Assert.That(clientSyncSet.IsProperSupersetOf(new[] { "World", "Hello" }));
        }

        [Test]
        public void TestIsSubsetOf()
        {
            Assert.That(clientSyncSet.IsSubsetOf(new[] { "World", "Hello", "!" }));
        }

        [Test]
        public void TestIsSupersetOf()
        {
            Assert.That(clientSyncSet.IsSupersetOf(new[] { "World", "Hello" }));
        }

        [Test]
        public void TestOverlaps()
        {
            Assert.That(clientSyncSet.Overlaps(new[] { "World", "my", "baby" }));
        }

        [Test]
        public void TestSetEquals()
        {
            Assert.That(clientSyncSet.SetEquals(new[] { "World", "Hello", "!" }));
        }

        [Test]
        public void TestSymmetricExceptWith()
        {
            serverSyncSet.SymmetricExceptWith(new HashSet<string> { "Hello", "is" });
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "World", "is", "!" }));
        }

        [Test]
        public void TestSymmetricExceptWithSelf()
        {
            serverSyncSet.SymmetricExceptWith(serverSyncSet);
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new String[] { }));
        }

        [Test]
        public void TestUnionWith()
        {
            serverSyncSet.UnionWith(new HashSet<string> { "Hello", "is" });
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "World", "Hello", "is", "!" }));
        }

        [Test]
        public void TestUnionWithSelf()
        {
            serverSyncSet.UnionWith(serverSyncSet);
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "World", "Hello", "!" }));
        }

        [Test]
        public void ReadOnlyTest()
        {
            Assert.That(serverSyncSet.IsReadOnly, Is.False);
            Assert.That(clientSyncSet.IsReadOnly, Is.True);
        }

        [Test]
        public void WritingToReadOnlyThrows()
        {
            Assert.Throws<InvalidOperationException>(() => { clientSyncSet.Add("5"); });
        }

        [Test]
        public void ObjectCanBeReusedAfterReset()
        {
            clientSyncSet.Reset();

            // make old client the host
            SyncSetString hostList = clientSyncSet;
            SyncSetString clientList2 = new SyncSetString();

            Assert.That(hostList.IsReadOnly, Is.False);

            // Check Add and Sync without errors
            hostList.Add("1");
            hostList.Add("2");
            hostList.Add("3");
            SerializeDeltaTo(hostList, clientList2);
        }

        [Test]
        public void ResetShouldSetReadOnlyToFalse()
        {
            clientSyncSet.Reset();

            Assert.That(clientSyncSet.IsReadOnly, Is.False);
        }

        [Test]
        public void ResetShouldClearChanges()
        {
            serverSyncSet.Reset();

            Assert.That(serverSyncSet.ChangeCount, Is.Zero);
        }

        [Test]
        public void ResetShouldClearItems()
        {
            serverSyncSet.Reset();

            Assert.That(serverSyncSet, Is.Empty);
        }
    }
}
