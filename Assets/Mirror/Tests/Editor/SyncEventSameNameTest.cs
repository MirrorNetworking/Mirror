using NUnit.Framework;


namespace Mirror.Tests.RemoteAttrributeTest
{
    public delegate void SomeEventDelegate(int someNumber);

    class EventBehaviour1 : NetworkBehaviour
    {
        [SyncEvent]
        public event SomeEventDelegate EventWithName;

        public void CallEvent(int i)
        {
            EventWithName.Invoke(i);
        }
    }

    class EventBehaviour2 : NetworkBehaviour
    {
        [SyncEvent]
        public event SomeEventDelegate EventWithName;

        public void CallEvent(int i)
        {
            EventWithName.Invoke(i);
        }
    }

    public class SyncEventSameNameTest : RemoteTestBase
    {
        [Test]
        public void EventsWithSameNameCanBothBeCalled()
        {
            EventBehaviour1 behaviour1 = CreateHostObject<EventBehaviour1>(true);
            EventBehaviour2 behaviour2 = CreateHostObject<EventBehaviour2>(true);

            const int someInt1 = 20;
            const int someInt2 = 21;

            int callCount1 = 0;
            int callCount2 = 0;
            behaviour1.EventWithName += incomingInt =>
            {
                callCount1++;
                Assert.That(incomingInt, Is.EqualTo(someInt1));
            };
            behaviour2.EventWithName += incomingInt =>
            {
                callCount2++;
                Assert.That(incomingInt, Is.EqualTo(someInt2));
            };

            behaviour1.CallEvent(someInt1);
            ProcessMessages();
            Assert.That(callCount1, Is.EqualTo(1));
            Assert.That(callCount2, Is.EqualTo(0));

            behaviour2.CallEvent(someInt2);
            ProcessMessages();
            Assert.That(callCount1, Is.EqualTo(1));
            Assert.That(callCount2, Is.EqualTo(1));
        }
    }
}
