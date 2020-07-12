using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.RemoteAttrributeTest
{
    public delegate void MySyncEventDelegate(int someNumber);
    public delegate void MySyncEventDelegate2(int someNumber, Vector3 somePosition);

    class SyncEventBehaviour : NetworkBehaviour
    {
        [SyncEvent]
        public event MySyncEventDelegate Only;

        public void CallEvent(int i)
        {
            Only.Invoke(i);
        }
    }

    class MultipleSyncEventBehaviour : NetworkBehaviour
    {
        [SyncEvent]
        public event MySyncEventDelegate EventFirst;

        [SyncEvent]
        public event MySyncEventDelegate2 EventSecond;

        public void CallEvent1(int i)
        {
            EventFirst?.Invoke(i);
        }

        public void CallEvent2(int i, Vector3 v)
        {
            EventSecond?.Invoke(i, v);
        }
    }

    public class SyncEventTest : RemoteTestBase
    {
        [Test]
        public void FirstEventIsCalled()
        {
            SyncEventBehaviour serverBehaviour = CreateHostObject<SyncEventBehaviour>(true);
            SyncEventBehaviour clientBehaviour = CreateHostObject<SyncEventBehaviour>(true);

            // set the clientBehaviour has the serverBehaviour's Id
            clientBehaviour.netIdentity.netId = serverBehaviour.netId;
            NetworkIdentity.spawned[serverBehaviour.netId] = clientBehaviour.netIdentity;

            const int someInt = 20;

            int callCount = 0;
            clientBehaviour.Only += incomingInt =>
            {
                callCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            serverBehaviour.CallEvent(someInt);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void SecondEventIsCalled()
        {
            MultipleSyncEventBehaviour serverBehaviour = CreateHostObject<MultipleSyncEventBehaviour>(true);
            MultipleSyncEventBehaviour clientBehaviour = CreateHostObject<MultipleSyncEventBehaviour>(true);

            // set the clientBehaviour has the serverBehaviour's Id
            clientBehaviour.netIdentity.netId = serverBehaviour.netId;
            NetworkIdentity.spawned[serverBehaviour.netId] = clientBehaviour.netIdentity;

            const int someInt1 = 20;
            const int someInt2 = 25;
            Vector3 someVector = Vector3.left;

            int callCount1 = 0;
            int callCount2 = 0;
            clientBehaviour.EventFirst += incomingInt =>
            {
                callCount1++;
                Assert.That(incomingInt, Is.EqualTo(someInt1));
            };
            clientBehaviour.EventSecond += (incomingInt, incomingVector) =>
            {
                callCount2++;
                Assert.That(incomingInt, Is.EqualTo(someInt2));
                Assert.That(incomingVector, Is.EqualTo(someVector));
            };

            serverBehaviour.CallEvent1(someInt1);
            ProcessMessages();
            Assert.That(callCount1, Is.EqualTo(1));
            Assert.That(callCount2, Is.EqualTo(0));

            serverBehaviour.CallEvent2(someInt2, someVector);
            ProcessMessages();
            Assert.That(callCount1, Is.EqualTo(1));
            Assert.That(callCount2, Is.EqualTo(1));
        }
    }
}
