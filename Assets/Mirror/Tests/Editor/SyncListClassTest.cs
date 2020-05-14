using NUnit.Framework;

namespace Mirror.Tests
{
    public class SyncListClassTest
    {
        [Test]
        public void RemoveShouldNotFail()
        {
            SyncListTestObject serverList = new SyncListTestObject();
            SyncListTestObject clientList = new SyncListTestObject();

            SyncListTest.SerializeAllTo(serverList, clientList);

            // add some items
            TestObject item1 = new TestObject { id = 1, text = "Lorem ipsum dolor sit, amet consectetur adipisicing elit. Nostrum ullam aliquid perferendis, aut nihil sunt quod ipsum corporis a. Cupiditate, alias. Commodi, molestiae distinctio repellendus dolor similique delectus inventore eum." };
            serverList.Add(item1);
            TestObject item2 = new TestObject { id = 2, text = "Lorem ipsum dolor sit, amet consectetur adipisicing elit. Nostrum ullam aliquid perferendis, aut nihil sunt quod ipsum corporis a. Cupiditate, alias. Commodi, molestiae distinctio repellendus dolor similique delectus inventore eum." };
            serverList.Add(item2);

            // sync
            SyncListTest.SerializeDeltaTo(serverList, clientList);


            // remove some items
            serverList.Remove(item1);

            // sync
            SyncListTest.SerializeDeltaTo(serverList, clientList);

            Assert.That(clientList.Contains(item1), Is.False);
            Assert.That(clientList.Contains(item2), Is.True);
        }
    }


    public class SyncListTestObject : SyncList<TestObject>
    {

    }
    public class TestObject
    {
        public int id;
        public string text;
    }
}
