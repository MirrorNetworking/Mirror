using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.RemoteAttrributeTest
{
    class TargetRpcBehaviour : NetworkBehaviour
    {
        public event Action<int> onSendInt;

        [TargetRpc]
        public void SendInt(int someInt)
        {
            onSendInt?.Invoke(someInt);
        }

        [TargetRpc]
        public void SendIntWithTarget(NetworkConnection target, int someInt)
        {
            onSendInt?.Invoke(someInt);
        }
    }

    public class TargetRpcTest : RemoteTestBase
    {
        [Test]
        public void TargetRpcIsCalled()
        {
            TargetRpcBehaviour hostBehaviour = CreateHostObject<TargetRpcBehaviour>(true);

            const int someInt = 20;

            int callCount = 0;
            hostBehaviour.onSendInt += incomingInt =>
            {
                callCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            hostBehaviour.SendInt(someInt);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void TargetRpcIsCalledOnTarget()
        {
            TargetRpcBehaviour hostBehaviour = CreateHostObject<TargetRpcBehaviour>(false);

            const int someInt = 20;

            int callCount = 0;
            hostBehaviour.onSendInt += incomingInt =>
            {
                callCount++;
                Assert.That(incomingInt, Is.EqualTo(someInt));
            };
            hostBehaviour.SendIntWithTarget(NetworkServer.localConnection, someInt);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void ErrorForTargetRpcWithNoOwner()
        {
            TargetRpcBehaviour hostBehaviour = CreateHostObject<TargetRpcBehaviour>(false);

            const int someInt = 20;

            hostBehaviour.onSendInt += incomingInt =>
            {
                Assert.Fail("Event should not be invoked with error");
            };
            LogAssert.Expect(UnityEngine.LogType.Error, $"TargetRPC {nameof(TargetRpcBehaviour.SendInt)} was given a null connection, make sure the object has an owner or you pass in the target connection");
            hostBehaviour.SendInt(someInt);
        }

        [Test]
        public void ErrorForTargetRpcWithNullArgment()
        {
            TargetRpcBehaviour hostBehaviour = CreateHostObject<TargetRpcBehaviour>(false);

            const int someInt = 20;

            hostBehaviour.onSendInt += incomingInt =>
            {
                Assert.Fail("Event should not be invoked with error");
            };
            LogAssert.Expect(UnityEngine.LogType.Error, $"TargetRPC {nameof(TargetRpcBehaviour.SendIntWithTarget)} was given a null connection, make sure the object has an owner or you pass in the target connection");
            hostBehaviour.SendIntWithTarget(null, someInt);
        }

        [Test]
        public void ErrorForTargetRpcWhenNotGivenConnectionToClient()
        {
            TargetRpcBehaviour hostBehaviour = CreateHostObject<TargetRpcBehaviour>(false);

            const int someInt = 20;

            hostBehaviour.onSendInt += incomingInt =>
            {
                Assert.Fail("Event should not be invoked with error");
            };
            LogAssert.Expect(UnityEngine.LogType.Error, $"TargetRPC {nameof(TargetRpcBehaviour.SendIntWithTarget)} requires a NetworkConnectionToClient but was given {typeof(FakeNetworkConnection).Name}");
            hostBehaviour.SendIntWithTarget(new FakeNetworkConnection(), someInt);
        }

        [Test]
        public void ErrorForTargetRpcWhenServerNotActive()
        {
            TargetRpcBehaviour hostBehaviour = CreateHostObject<TargetRpcBehaviour>(true);

            const int someInt = 20;

            hostBehaviour.onSendInt += incomingInt =>
            {
                Assert.Fail("Event should not be invoked with error");
            };
            NetworkServer.active = false;
            LogAssert.Expect(UnityEngine.LogType.Error, $"TargetRPC {nameof(TargetRpcBehaviour.SendInt)} called when server not active");
            hostBehaviour.SendInt(someInt);
        }

        [Test]
        public void ErrorForTargetRpcWhenObjetNotSpawned()
        {
            GameObject gameObject = new GameObject();
            spawned.Add(gameObject);
            gameObject.AddComponent<NetworkIdentity>();
            TargetRpcBehaviour hostBehaviour = gameObject.AddComponent<TargetRpcBehaviour>();

            const int someInt = 20;

            hostBehaviour.onSendInt += incomingInt =>
            {
                Assert.Fail("Event should not be invoked with error");
            };
            LogAssert.Expect(UnityEngine.LogType.Error, $"TargetRpc {nameof(TargetRpcBehaviour.SendInt)} called on {hostBehaviour.name} but that object has not been spawned or has been unspawned");
            hostBehaviour.SendInt(someInt);
        }
    }
}
