using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Mirror.Tests
{
    [TestFixture]
    public class SyncSetTest
    {
        SyncHashSet<string> serverSyncSet;
        SyncHashSet<string> clientSyncSet;
        int serverSyncSetDirtyCalled;
        int clientSyncSetDirtyCalled;

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
            serverSyncSet = new SyncHashSet<string>();
            clientSyncSet = new SyncHashSet<string>();

            // add some data to the list
            serverSyncSet.Add("Hello");
            serverSyncSet.Add("World");
            serverSyncSet.Add("!");
            SerializeAllTo(serverSyncSet, clientSyncSet);

            // set up dirty callbacks for testing
            // AFTER adding the example data. we already know we added that data.
            serverSyncSet.OnDirty = () => ++serverSyncSetDirtyCalled;
            clientSyncSet.OnDirty = () => ++clientSyncSetDirtyCalled;
            serverSyncSetDirtyCalled = 0;
            clientSyncSetDirtyCalled = 0;
        }

        [Test]
        public void TestInit()
        {
            Assert.That(serverSyncSet, Is.EquivalentTo(new[] { "Hello", "World", "!" }));
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "Hello", "World", "!" }));
        }

        // test the '= List<int>{1,2,3}' constructor.
        // it calls .Add(1); .Add(2); .Add(3) in the constructor.
        // (the OnDirty change broke this and we didn't have a test before)
        [Test]
        public void CurlyBracesConstructor()
        {
            SyncHashSet<int> set = new SyncHashSet<int>{1,2,3};
            Assert.That(set.Count, Is.EqualTo(3));
        }

        [Test]
        public void TestAdd()
        {
            serverSyncSet.Add("yay");
            Assert.That(serverSyncSetDirtyCalled, Is.EqualTo(1));
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "Hello", "World", "!", "yay" }));
        }

        [Test]
        public void TestClear()
        {
            serverSyncSet.Clear();
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new string[] {}));
        }

        [Test]
        public void TestRemove()
        {
            serverSyncSet.Remove("World");
            Assert.That(serverSyncSetDirtyCalled, Is.EqualTo(1));
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "Hello", "!" }));
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
        public void CallbackTest()
        {
            bool called = false;

            clientSyncSet.Callback += (op, item) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncHashSet<string>.Operation.OP_ADD));
                Assert.That(item, Is.EqualTo("yay"));
            };

            serverSyncSet.Add("yay");
            SerializeDeltaTo(serverSyncSet, clientSyncSet);

            Assert.That(called, Is.True);
        }

        [Test]
        public void CallbackRemoveTest()
        {
            bool called = false;

            clientSyncSet.Callback += (op, item) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncHashSet<string>.Operation.OP_REMOVE));
                Assert.That(item, Is.EqualTo("World"));
            };
            serverSyncSet.Remove("World");
            SerializeDeltaTo(serverSyncSet, clientSyncSet);

            Assert.That(called, Is.True);
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
            Assert.That(clientSyncSet, Is.EquivalentTo(new String[] {}));
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
            Assert.That(clientSyncSet, Is.EquivalentTo(new String[] {}));
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
            SyncHashSet<string> hostList = clientSyncSet;
            SyncHashSet<string> clientList2 = new SyncHashSet<string>();

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
            Assert.That(serverSyncSet.GetChangeCount(), Is.Zero);
        }

        [Test]
        public void ResetShouldClearItems()
        {
            serverSyncSet.Reset();
            Assert.That(serverSyncSet, Is.Empty);
        }

        [Test]
        public void IsRecording()
        {
            // shouldn't record changes if IsRecording() returns false
            serverSyncSet.ClearChanges();
            serverSyncSet.IsRecording = () => false;
            serverSyncSet.Add("42");
            Assert.That(serverSyncSet.GetChangeCount(), Is.EqualTo(0));
        }
    }
}
