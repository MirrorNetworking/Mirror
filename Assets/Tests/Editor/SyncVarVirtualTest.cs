using System;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    abstract class SyncVarHookTesterBase : NetworkBehaviour
    {
        [SyncVar(hook = nameof(onValue1Changed))]
        public float value1;
        [SyncVar(hook = nameof(onValue2Changed))]
        public float value2;

        public event Action OnValue2ChangedVirtualCalled;

        public abstract void onValue1Changed(float old, float newValue);
        public virtual void onValue2Changed(float old, float newValue)
        {
            OnValue2ChangedVirtualCalled?.Invoke();
        }

        public void ChangeValues()
        {
            value1 += 1f;
            value2 += 1f;
        }

        public void CallOnValue2Changed()
        {
            onValue2Changed(1, 1);
        }
    }

    class SyncVarHookTester : SyncVarHookTesterBase
    {
        public event Action OnValue1ChangedOverrideCalled;
        public event Action OnValue2ChangedOverrideCalled;
        public override void onValue1Changed(float old, float newValue)
        {
            OnValue1ChangedOverrideCalled?.Invoke();
        }
        public override void onValue2Changed(float old, float newValue)
        {
            OnValue2ChangedOverrideCalled?.Invoke();
        }
    }
    [TestFixture]
    public class SyncVarVirtualTest
    {
        private SyncVarHookTester serverTester;
        private NetworkIdentity netIdServer;
        private SyncVarHookTester clientTester;
        private NetworkIdentity netIdClient;

        [SetUp]
        public void Setup()
        {
            // create server and client objects and sync inital values

            var gameObject1 = new GameObject();
            netIdServer = gameObject1.AddComponent<NetworkIdentity>();
            serverTester = gameObject1.AddComponent<SyncVarHookTester>();

            var gameObject2 = new GameObject();
            netIdClient = gameObject2.AddComponent<NetworkIdentity>();
            clientTester = gameObject2.AddComponent<SyncVarHookTester>();

            serverTester.value1 = 1;
            serverTester.value2 = 2;

            SyncValuesWithClient();
        }

        private void SyncValuesWithClient()
        {
            // serialize all the data as we would for the network
            var ownerWriter = new NetworkWriter();
            // not really used in this Test
            var observersWriter = new NetworkWriter();

            netIdServer.OnSerializeAllSafely(true, ownerWriter, observersWriter);

            // apply all the data from the server object
            var reader = new NetworkReader(ownerWriter.ToArray());
            netIdClient.OnDeserializeAllSafely(reader, true);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(serverTester.gameObject);
            UnityEngine.Object.DestroyImmediate(clientTester.gameObject);
        }
        [Test]
        public void abstractMethodOnChangeWorkWithHooks()
        {
            serverTester.ChangeValues();

            bool value1OverrideCalled = false;
            clientTester.OnValue1ChangedOverrideCalled += () =>
            {
                value1OverrideCalled = true;
            };

            SyncValuesWithClient();

            Assert.AreEqual(serverTester.value1, serverTester.value1);
            Assert.IsTrue(value1OverrideCalled);
        }
        [Test]
        public void virtualMethodOnChangeWorkWithHooks()
        {
            serverTester.ChangeValues();

            bool value2OverrideCalled = false;
            clientTester.OnValue2ChangedOverrideCalled += () =>
            {
                value2OverrideCalled = true;
            };

            bool value2VirtualCalled = false;
            clientTester.OnValue2ChangedVirtualCalled += () =>
            {
                value2VirtualCalled = true;
            };

            SyncValuesWithClient();

            Assert.AreEqual(serverTester.value2, serverTester.value2);
            Assert.IsTrue(value2OverrideCalled, "Override method not called");
            Assert.IsFalse(value2VirtualCalled, "Virtual method called when Override exists");
        }

        [Test]
        public void manuallyCallingVirtualMethodCallsOverride()
        {
            // this to check that class are set up correct for tests above
            serverTester.ChangeValues();

            bool value2OverrideCalled = false;
            clientTester.OnValue2ChangedOverrideCalled += () =>
            {
                value2OverrideCalled = true;
            };

            bool value2VirtualCalled = false;
            clientTester.OnValue2ChangedVirtualCalled += () =>
            {
                value2VirtualCalled = true;
            };

            var baseClass = clientTester as SyncVarHookTesterBase;
            baseClass.onValue2Changed(1, 1);

            Assert.AreEqual(serverTester.value2, serverTester.value2);
            Assert.IsTrue(value2OverrideCalled, "Override method not called");
            Assert.IsFalse(value2VirtualCalled, "Virtual method called when Override exists");
        }
        [Test]
        public void manuallyCallingVirtualMethodInsideBaseClassCallsOverride()
        {
            // this to check that class are set up correct for tests above
            serverTester.ChangeValues();

            bool value2OverrideCalled = false;
            clientTester.OnValue2ChangedOverrideCalled += () =>
            {
                value2OverrideCalled = true;
            };

            bool value2VirtualCalled = false;
            clientTester.OnValue2ChangedVirtualCalled += () =>
            {
                value2VirtualCalled = true;
            };

            var baseClass = clientTester as SyncVarHookTesterBase;
            baseClass.CallOnValue2Changed();

            Assert.AreEqual(serverTester.value2, serverTester.value2);
            Assert.IsTrue(value2OverrideCalled, "Override method not called");
            Assert.IsFalse(value2VirtualCalled, "Virtual method called when Override exists");
        }
    }
}
