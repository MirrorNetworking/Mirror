using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
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


    public class SyncVarHookTest
    {
        private List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject item in spawned)
            {
                GameObject.DestroyImmediate(item);
            }
            spawned.Clear();

            NetworkIdentity.spawned.Clear();
        }


        T CreateObject<T>() where T : NetworkBehaviour
        {
            GameObject gameObject = new GameObject();
            spawned.Add(gameObject);

            gameObject.AddComponent<NetworkIdentity>();

            T behaviour = gameObject.AddComponent<T>();
            behaviour.syncInterval = 0f;

            return behaviour;
        }

        NetworkIdentity CreateNetworkIdentity(uint netId)
        {
            GameObject gameObject = new GameObject();
            spawned.Add(gameObject);

            NetworkIdentity networkIdentity = gameObject.AddComponent<NetworkIdentity>();
            networkIdentity.netId = netId;
            NetworkIdentity.spawned[netId] = networkIdentity;
            return networkIdentity;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serverObject"></param>
        /// <param name="clientObject"></param>
        /// <param name="initialState"></param>
        /// <returns>If data was written by OnSerialize</returns>
        public static bool SyncToClient<T>(T serverObject, T clientObject, bool initialState) where T : NetworkBehaviour
        {
            NetworkWriter writer = new NetworkWriter();
            bool written = serverObject.OnSerialize(writer, initialState);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            clientObject.OnDeserialize(reader, initialState);

            int writeLength = writer.Length;
            int readLength = reader.Position;
            Assert.That(writeLength == readLength, $"OnSerializeAll and OnDeserializeAll calls write the same amount of data\n    writeLength={writeLength}\n    readLength={readLength}");

            return written;
        }

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
