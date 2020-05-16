using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    class NewHookBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnValueChnaged))]
        public int value = 0;

        public event Action<int> HookCalled;

        void OnValueChnaged(int newValue)
        {
            HookCalled.Invoke(newValue);
        }
    }

    class OldNewHookBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnValueChnaged))]
        public int value = 0;

        public event Action<int, int> HookCalled;

        void OnValueChnaged(int oldValue, int newValue)
        {
            HookCalled.Invoke(oldValue, newValue);
        }
    }

    class OldNewInitialHookBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnValueChnaged))]
        public int value = 0;

        public event Action<int, int, bool> HookCalled;

        void OnValueChnaged(int oldValue, int newValue, bool intialState)
        {
            HookCalled.Invoke(oldValue, newValue, intialState);
        }
    }

    class MultipleOverloadsHookBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnValueChnaged), hookParameter = HookParameter.New)]
        public int value = 0;

        public event Action<int> HookCalled;
        public event Action<int> NotHookCalled;

        void OnValueChnaged(int newValue)
        {
            HookCalled.Invoke(newValue);
        }

        void OnValueChnaged(int newValue, int someOtherValue)
        {
            NotHookCalled.Invoke(newValue);
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
        public void New_HookCalledWhenSyncingChangedValue(bool intialState)
        {
            NewHookBehaviour serverObject = CreateObject<NewHookBehaviour>();
            NewHookBehaviour clientObject = CreateObject<NewHookBehaviour>();

            const int clientValue = 10;
            const int serverValue = 24;

            serverObject.value = serverValue;
            clientObject.value = clientValue;

            int callCount = 0;
            clientObject.HookCalled += newValue =>
            {
                callCount++;
                Assert.That(newValue, Is.EqualTo(serverValue));
            };

            bool written = SyncToClient(serverObject, clientObject, intialState);
            Assert.IsTrue(written);
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void OldNew_HookCalledWhenSyncingChangedValue(bool intialState)
        {
            OldNewHookBehaviour serverObject = CreateObject<OldNewHookBehaviour>();
            OldNewHookBehaviour clientObject = CreateObject<OldNewHookBehaviour>();

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
        public void OldNewInitial_HookCalledWhenSyncingChangedValue(bool intialState)
        {
            OldNewInitialHookBehaviour serverObject = CreateObject<OldNewInitialHookBehaviour>();
            OldNewInitialHookBehaviour clientObject = CreateObject<OldNewInitialHookBehaviour>();

            const int clientValue = 10;
            const int serverValue = 24;

            serverObject.value = serverValue;
            clientObject.value = clientValue;

            int callCount = 0;
            clientObject.HookCalled += (oldValue, newValue, intial) =>
            {
                callCount++;
                Assert.That(oldValue, Is.EqualTo(clientValue));
                Assert.That(newValue, Is.EqualTo(serverValue));
                Assert.That(intial, Is.EqualTo(intialState));
            };

            bool written = SyncToClient(serverObject, clientObject, intialState);
            Assert.IsTrue(written);
            Assert.That(callCount, Is.EqualTo(1));
        }


        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void New_HookNotCalledWhenSyncingSameValue(bool intialState)
        {
            NewHookBehaviour serverObject = CreateObject<NewHookBehaviour>();
            NewHookBehaviour clientObject = CreateObject<NewHookBehaviour>();

            const int clientValue = 16;
            const int serverValue = 16;

            serverObject.value = serverValue;
            clientObject.value = clientValue;

            int callCount = 0;
            clientObject.HookCalled += newValue =>
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
        public void OldNew_HookNotCalledWhenSyncingSameValue(bool intialState)
        {
            OldNewHookBehaviour serverObject = CreateObject<OldNewHookBehaviour>();
            OldNewHookBehaviour clientObject = CreateObject<OldNewHookBehaviour>();

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
        public void OldNewInitial_HookOnlyCalledWhenSyncingSameValueIfIntial(bool intialState)
        {
            OldNewInitialHookBehaviour serverObject = CreateObject<OldNewInitialHookBehaviour>();
            OldNewInitialHookBehaviour clientObject = CreateObject<OldNewInitialHookBehaviour>();

            const int clientValue = 16;
            const int serverValue = 16;

            serverObject.value = serverValue;
            clientObject.value = clientValue;

            int callCount = 0;
            clientObject.HookCalled += (oldValue, newValue, intial) =>
            {
                callCount++;
                Assert.That(oldValue, Is.EqualTo(clientValue));
                Assert.That(newValue, Is.EqualTo(serverValue));
                Assert.That(intial, Is.EqualTo(intialState));
            };

            bool written = SyncToClient(serverObject, clientObject, intialState);
            Assert.IsTrue(written);
            Assert.That(callCount, Is.EqualTo(intialState ? 1 : 0));
        }


        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void MultipleMethods_HookCalledWhenSyncingChangedValue(bool intialState)
        {
            MultipleOverloadsHookBehaviour serverObject = CreateObject<MultipleOverloadsHookBehaviour>();
            MultipleOverloadsHookBehaviour clientObject = CreateObject<MultipleOverloadsHookBehaviour>();

            const int clientValue = 10;
            const int serverValue = 24;

            serverObject.value = serverValue;
            clientObject.value = clientValue;

            int hookcallCount = 0;
            int notCallCount = 0;
            clientObject.HookCalled += newValue =>
            {
                hookcallCount++;
            };
            clientObject.NotHookCalled += newValue =>
            {
                notCallCount++;
            };

            bool written = SyncToClient(serverObject, clientObject, intialState);
            Assert.IsTrue(written);
            Assert.That(hookcallCount, Is.EqualTo(1));
            Assert.That(notCallCount, Is.EqualTo(1));
        }
    }
}
