using System;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.SyncVarTests
{
    class HookBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnValueChanged))]
        public int value = 0;

        public event Action<int, int> HookCalled;

        void OnValueChanged(int oldValue, int newValue)
        {
            HookCalled.Invoke(oldValue, newValue);
        }
    }

    class GameObjectHookBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnValueChanged))]
        public GameObject value = null;

        public event Action<GameObject, GameObject> HookCalled;

        void OnValueChanged(GameObject oldValue, GameObject newValue)
        {
            HookCalled.Invoke(oldValue, newValue);
        }
    }

    class NetworkIdentityHookBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnValueChanged))]
        public NetworkIdentity value = null;

        public event Action<NetworkIdentity, NetworkIdentity> HookCalled;

        void OnValueChanged(NetworkIdentity oldValue, NetworkIdentity newValue)
        {
            HookCalled.Invoke(oldValue, newValue);
        }
    }

    class NetworkBehaviourHookBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnValueChanged))]
        public NetworkBehaviourHookBehaviour value = null;

        public event Action<NetworkBehaviourHookBehaviour, NetworkBehaviourHookBehaviour> HookCalled;

        void OnValueChanged(NetworkBehaviourHookBehaviour oldValue, NetworkBehaviourHookBehaviour newValue)
        {
            HookCalled.Invoke(oldValue, newValue);
        }
    }


    class StaticHookBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnValueChanged))]
        public int value = 0;

        public static event Action<int, int> HookCalled;

        static void OnValueChanged(int oldValue, int newValue)
        {
            HookCalled.Invoke(oldValue, newValue);
        }
    }

    class VirtualHookBase : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnValueChanged))]
        public int value = 0;

        public event Action<int, int> BaseHookCalled;

        protected virtual void OnValueChanged(int oldValue, int newValue)
        {
            BaseHookCalled.Invoke(oldValue, newValue);
        }
    }

    class VirtualOverrideHook : VirtualHookBase
    {
        public event Action<int, int> OverrideHookCalled;

        protected override void OnValueChanged(int oldValue, int newValue)
        {
            OverrideHookCalled.Invoke(oldValue, newValue);
        }
    }

    abstract class AbstractHookBase : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnValueChanged))]
        public int value = 0;

        protected abstract void OnValueChanged(int oldValue, int newValue);
    }

    class AbstractHook : AbstractHookBase
    {
        public event Action<int, int> HookCalled;

        protected override void OnValueChanged(int oldValue, int newValue)
        {
            HookCalled.Invoke(oldValue, newValue);
        }
    }


    public class SyncVarHookTest : SyncVarTestBase
    {
        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void Hook_CalledWhenSyncingChangedValue(bool intialState)
        {
            HookBehaviour serverObject = CreateObject<HookBehaviour>();
            HookBehaviour clientObject = CreateObject<HookBehaviour>();

            const int clientValue = 10;
            const int serverValue = 24;

            serverObject.value = serverValue;
            clientObject.value = clientValue;

            int callCount = 0;
            clientObject.HookCalled += (oldValue, newValue) =>
            {
                callCount++;
                Assert.That(oldValue, Is.EqualTo(clientValue));
                Assert.That(newValue, Is.EqualTo(serverValue));
            };

            bool written = SyncToClient(serverObject, clientObject, intialState);
            Assert.IsTrue(written);
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void Hook_NotCalledWhenSyncingSameValue(bool intialState)
        {
            HookBehaviour serverObject = CreateObject<HookBehaviour>();
            HookBehaviour clientObject = CreateObject<HookBehaviour>();

            const int clientValue = 16;
            const int serverValue = 16;

            serverObject.value = serverValue;
            clientObject.value = clientValue;

            int callCount = 0;
            clientObject.HookCalled += (oldValue, newValue) =>
            {
                callCount++;
            };

            bool written = SyncToClient(serverObject, clientObject, intialState);
            Assert.IsTrue(written);
            Assert.That(callCount, Is.EqualTo(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void StaticMethod_HookCalledWhenSyncingChangedValue(bool intialState)
        {
            StaticHookBehaviour serverObject = CreateObject<StaticHookBehaviour>();
            StaticHookBehaviour clientObject = CreateObject<StaticHookBehaviour>();

            const int clientValue = 10;
            const int serverValue = 24;

            serverObject.value = serverValue;
            clientObject.value = clientValue;

            int hookcallCount = 0;
            StaticHookBehaviour.HookCalled += (oldValue, newValue) =>
            {
                hookcallCount++;
                Assert.That(oldValue, Is.EqualTo(clientValue));
                Assert.That(newValue, Is.EqualTo(serverValue));
            };

            bool written = SyncToClient(serverObject, clientObject, intialState);
            Assert.IsTrue(written);
            Assert.That(hookcallCount, Is.EqualTo(1));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void GameObjectHook_HookCalledWhenSyncingChangedValue(bool intialState)
        {
            GameObjectHookBehaviour serverObject = CreateObject<GameObjectHookBehaviour>();
            GameObjectHookBehaviour clientObject = CreateObject<GameObjectHookBehaviour>();

            GameObject clientValue = null;
            GameObject serverValue = CreateNetworkIdentity(2032).gameObject;

            serverObject.value = serverValue;
            clientObject.value = clientValue;

            int callCount = 0;
            clientObject.HookCalled += (oldValue, newValue) =>
            {
                callCount++;
                Assert.That(oldValue, Is.EqualTo(clientValue));
                Assert.That(newValue, Is.EqualTo(serverValue));
            };

            bool written = SyncToClient(serverObject, clientObject, intialState);
            Assert.IsTrue(written);
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void NetworkIdentityHook_HookCalledWhenSyncingChangedValue(bool intialState)
        {
            NetworkIdentityHookBehaviour serverObject = CreateObject<NetworkIdentityHookBehaviour>();
            NetworkIdentityHookBehaviour clientObject = CreateObject<NetworkIdentityHookBehaviour>();

            NetworkIdentity clientValue = null;
            NetworkIdentity serverValue = CreateNetworkIdentity(2033);

            serverObject.value = serverValue;
            clientObject.value = clientValue;

            int callCount = 0;
            clientObject.HookCalled += (oldValue, newValue) =>
            {
                callCount++;
                Assert.That(oldValue, Is.EqualTo(clientValue));
                Assert.That(newValue, Is.EqualTo(serverValue));
            };

            bool written = SyncToClient(serverObject, clientObject, intialState);
            Assert.IsTrue(written);
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void NetworkBehaviourHook_HookCalledWhenSyncingChangedValue(bool intialState)
        {
            NetworkBehaviourHookBehaviour serverObject = CreateObject<NetworkBehaviourHookBehaviour>();
            NetworkBehaviourHookBehaviour clientObject = CreateObject<NetworkBehaviourHookBehaviour>();

            NetworkBehaviourHookBehaviour clientValue = null;
            NetworkBehaviourHookBehaviour serverValue = CreateNetworkIdentity(2033).gameObject.AddComponent<NetworkBehaviourHookBehaviour>();

            serverObject.value = serverValue;
            clientObject.value = clientValue;

            int callCount = 0;
            clientObject.HookCalled += (oldValue, newValue) =>
            {
                callCount++;
                Assert.That(oldValue, Is.EqualTo(clientValue));
                Assert.That(newValue, Is.EqualTo(serverValue));
            };

            bool written = SyncToClient(serverObject, clientObject, intialState);
            Assert.IsTrue(written);
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void VirtualHook_HookCalledWhenSyncingChangedValue(bool intialState)
        {
            VirtualHookBase serverObject = CreateObject<VirtualHookBase>();
            VirtualHookBase clientObject = CreateObject<VirtualHookBase>();

            const int clientValue = 10;
            const int serverValue = 24;

            serverObject.value = serverValue;
            clientObject.value = clientValue;

            int baseCallCount = 0;
            clientObject.BaseHookCalled += (oldValue, newValue) =>
            {
                baseCallCount++;
                Assert.That(oldValue, Is.EqualTo(clientValue));
                Assert.That(newValue, Is.EqualTo(serverValue));
            };

            bool written = SyncToClient(serverObject, clientObject, intialState);
            Assert.IsTrue(written);
            Assert.That(baseCallCount, Is.EqualTo(1));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void VirtualOverrideHook_HookCalledWhenSyncingChangedValue(bool intialState)
        {
            VirtualOverrideHook serverObject = CreateObject<VirtualOverrideHook>();
            VirtualOverrideHook clientObject = CreateObject<VirtualOverrideHook>();

            const int clientValue = 10;
            const int serverValue = 24;

            serverObject.value = serverValue;
            clientObject.value = clientValue;

            int overrideCallCount = 0;
            int baseCallCount = 0;
            clientObject.OverrideHookCalled += (oldValue, newValue) =>
            {
                overrideCallCount++;
                Assert.That(oldValue, Is.EqualTo(clientValue));
                Assert.That(newValue, Is.EqualTo(serverValue));
            };
            clientObject.BaseHookCalled += (oldValue, newValue) =>
            {
                baseCallCount++;
            };

            bool written = SyncToClient(serverObject, clientObject, intialState);
            Assert.IsTrue(written);
            Assert.That(overrideCallCount, Is.EqualTo(1));
            Assert.That(baseCallCount, Is.EqualTo(0));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void AbstractHook_HookCalledWhenSyncingChangedValue(bool intialState)
        {
            AbstractHook serverObject = CreateObject<AbstractHook>();
            AbstractHook clientObject = CreateObject<AbstractHook>();

            const int clientValue = 10;
            const int serverValue = 24;

            serverObject.value = serverValue;
            clientObject.value = clientValue;

            int callCount = 0;
            clientObject.HookCalled += (oldValue, newValue) =>
            {
                callCount++;
                Assert.That(oldValue, Is.EqualTo(clientValue));
                Assert.That(newValue, Is.EqualTo(serverValue));
            };

            bool written = SyncToClient(serverObject, clientObject, intialState);
            Assert.IsTrue(written);
            Assert.That(callCount, Is.EqualTo(1));
        }
    }
}
