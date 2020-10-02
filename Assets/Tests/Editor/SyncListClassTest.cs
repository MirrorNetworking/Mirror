using System.Linq;
using NUnit.Framework;

namespace Mirror.Tests
{
    class TestObjectBehaviour : NetworkBehaviour
    {
        // note synclists must be a property of a NetworkBehavior so that
        // the weaver generates the reader and writer for the object
        public SyncList<TestObject> myList = new SyncList<TestObject>();
    }

    public class SyncListClassTest
    {
        [Test]
        public void RemoveShouldRemoveItem()
        {
            SyncList<TestObject> serverList = new SyncList<TestObject>();
            SyncList<TestObject> clientList = new SyncList<TestObject>();

            SyncListTest.SerializeAllTo(serverList, clientList);

            // add some items
            TestObject item1 = new TestObject { id = 1, text = "Lorem ipsum dolor sit, amet consectetur adipisicing elit. Nostrum ullam aliquid perferendis, aut nihil sunt quod ipsum corporis a. Cupiditate, alias. Commodi, molestiae distinctio repellendus dolor similique delectus inventore eum." };
            serverList.Add(item1);
            TestObject item2 = new TestObject { id = 2, text = "Lorem ipsum dolor sit, amet consectetur adipisicing elit. Nostrum ullam aliquid perferendis, aut nihil sunt quod ipsum corporis a. Cupiditate, alias. Commodi, molestiae distinctio repellendus dolor similique delectus inventore eum." };
            serverList.Add(item2);

            // sync
            SyncListTest.SerializeDeltaTo(serverList, clientList);

            // clear all items            
            serverList.Remove(item1);

            // sync
            SyncListTest.SerializeDeltaTo(serverList, clientList);

            Assert.IsFalse(clientList.Any(x => x.id == item1.id));
            Assert.IsTrue(clientList.Any(x => x.id == item2.id));
        }

        [Test]
        public void ClearShouldClearAll()
        {
            SyncList<TestObject> serverList = new SyncList<TestObject>();
            SyncList<TestObject> clientList = new SyncList<TestObject>();

            SyncListTest.SerializeAllTo(serverList, clientList);

            // add some items
            TestObject item1 = new TestObject { id = 1, text = "Lorem ipsum dolor sit, amet consectetur adipisicing elit. Nostrum ullam aliquid perferendis, aut nihil sunt quod ipsum corporis a. Cupiditate, alias. Commodi, molestiae distinctio repellendus dolor similique delectus inventore eum." };
            serverList.Add(item1);
            TestObject item2 = new TestObject { id = 2, text = "Lorem ipsum dolor sit, amet consectetur adipisicing elit. Nostrum ullam aliquid perferendis, aut nihil sunt quod ipsum corporis a. Cupiditate, alias. Commodi, molestiae distinctio repellendus dolor similique delectus inventore eum." };
            serverList.Add(item2);

            // sync
            SyncListTest.SerializeDeltaTo(serverList, clientList);

            // clear all items            
            serverList.Clear();

            // sync
            SyncListTest.SerializeDeltaTo(serverList, clientList);

            Assert.That(clientList.Count, Is.Zero);

            Assert.IsFalse(clientList.Any(x => x.id == item1.id));
            Assert.IsFalse(clientList.Any(x => x.id == item2.id));
        }
    }

    [System.Serializable]
    public class TestObject
    {
        public int id;
        public string text;
    }
}
