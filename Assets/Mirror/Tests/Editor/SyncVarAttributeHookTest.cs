using System;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.SyncVarAttributeTests
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

    public struct Proportions
    {
        public byte[] Array;
    }
    class ImerHook_Ldflda : NetworkBehaviour
    {
        // to check
        public byte[] ldflda_Array;

        [SyncVar(hook = nameof(OnUpdateProportions))]
        public Proportions _syncProportions;

        protected void OnUpdateProportions(Proportions old, Proportions new_)
        {
            // _new is fine with the new values.
            // assigning to _syncProportions is fine too.
            _syncProportions = new_;

            // loading _syncProportions.Array would still load the original SyncVar,
            // not the replacement. so .Array was still null.
            // we needed to replace ldflda here.
            //
            // this throws if it still loads the old _syncProportions after weaving
            // because the .Array was still null there.
            ldflda_Array = _syncProportions.Array;
            Debug.Log("Array= " + ldflda_Array);
        }
    }


    // repro for the bug found by David_548219 in discord where setting
    // MyStruct.value would throw invalid IL
    public struct DavidStruct
    {
        public int Value;
    }
    class DavidHookComponent : NetworkBehaviour
    {
        [SyncVar] public DavidStruct syncvar;

        public override void OnStartServer()
        {
            syncvar.Value = 42;
        }
    }

    public class SyncVarAttributeHookTest : MirrorTest
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // start server & connect client because we need spawn functions
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out _);
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        [Test]
        public void Hook_CalledWhenSyncingChangedValued()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out HookBehaviour serverObject,
                out _, out _, out HookBehaviour clientObject);

            const int serverValue = 24;

            // change it on server
            serverObject.value = serverValue;

            // hook should change it on client
            int callCount = 0;
            clientObject.HookCalled += (oldValue, newValue) =>
            {
                callCount++;
                Assert.That(oldValue, Is.EqualTo(0));
                Assert.That(newValue, Is.EqualTo(serverValue));
            };

            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void Hook_NotCalledWhenSyncingSameValued()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out HookBehaviour serverObject,
                out _, out _, out HookBehaviour clientObject);

            const int clientValue = 16;
            const int serverValue = 16;

            // set both to same values once
            serverObject.value = serverValue;
            clientObject.value = clientValue;

            // client hook
            int callCount = 0;
            clientObject.HookCalled += (oldValue, newValue) =>
            {
                callCount++;
            };

            // hook shouldn't be called because both already have same value
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(0));
        }

        [Test]
        public void Hook_OnlyCalledOnClientd()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out HookBehaviour serverObject,
                out _, out _, out HookBehaviour clientObject);

            // set up hooks on server and client
            int clientCalled = 0;
            int serverCalled = 0;
            clientObject.HookCalled += (oldValue, newValue) => ++clientCalled;
            serverObject.HookCalled += (oldValue, newValue) => ++serverCalled;

            // change on server
            ++serverObject.value;

            // sync. hook should've only been called on client.
            ProcessMessages();
            Assert.That(clientCalled, Is.EqualTo(1));
            Assert.That(serverCalled, Is.EqualTo(0));
        }

        [Test]
        public void StaticMethod_HookCalledWhenSyncingChangedValued()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out StaticHookBehaviour serverObject,
                out _, out _, out StaticHookBehaviour clientObject);

            const int serverValue = 24;

            // change it on server
            serverObject.value = serverValue;

            // hook should change it on client
            int hookcallCount = 0;
            StaticHookBehaviour.HookCalled += (oldValue, newValue) =>
            {
                hookcallCount++;
                Assert.That(oldValue, Is.EqualTo(0));
                Assert.That(newValue, Is.EqualTo(serverValue));
            };

            ProcessMessages();
            Assert.That(hookcallCount, Is.EqualTo(1));
        }

        [Test]
        public void GameObjectHook_HookCalledWhenSyncingChangedValued()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out GameObjectHookBehaviour serverObject,
                out _, out _, out GameObjectHookBehaviour clientObject);

            // create spawned because we will look up netId in .spawned
            CreateNetworkedAndSpawn(
                out GameObject serverValue, out _,
                out GameObject clientValue, out _);

            // change it on server
            clientObject.value = null;
            serverObject.value = serverValue;

            // hook should change it on client
            int callCount = 0;
            clientObject.HookCalled += (oldValue, newValue) =>
            {
                callCount++;
                Assert.That(oldValue, Is.EqualTo(null));
                Assert.That(newValue, Is.EqualTo(clientValue));
            };

            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void NetworkIdentityHook_HookCalledWhenSyncingChangedValued()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out NetworkIdentityHookBehaviour serverObject,
                out _, out _, out NetworkIdentityHookBehaviour clientObject);

            // create spawned because we will look up netId in .spawned
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverValue,
                out _, out NetworkIdentity clientValue);

            // change it on server
            serverObject.value = serverValue;
            clientObject.value = null;

            // hook should change it on client
            int callCount = 0;
            clientObject.HookCalled += (oldValue, newValue) =>
            {
                callCount++;
                Assert.That(oldValue, Is.EqualTo(null));
                Assert.That(newValue, Is.EqualTo(clientValue));
            };

            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void NetworkBehaviourHook_HookCalledWhenSyncingChangedValued()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out NetworkBehaviourHookBehaviour serverObject,
                out _, out _, out NetworkBehaviourHookBehaviour clientObject);

            // create spawned because we will look up netId in .spawned
            CreateNetworkedAndSpawn(
                out _, out _, out NetworkBehaviourHookBehaviour serverValue,
                out _, out _, out NetworkBehaviourHookBehaviour clientValue);

            // change it on server
            serverObject.value = serverValue;
            clientObject.value = null;

            // hook should change it on client
            int callCount = 0;
            clientObject.HookCalled += (oldValue, newValue) =>
            {
                callCount++;
                Assert.That(oldValue, Is.EqualTo(null));
                Assert.That(newValue, Is.EqualTo(clientValue));
            };

            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void VirtualHook_HookCalledWhenSyncingChangedValued()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out VirtualHookBase serverObject,
                out _, out _, out VirtualHookBase clientObject);

            const int clientValue = 10;
            const int serverValue = 24;

            // change it on server
            serverObject.value = serverValue;
            clientObject.value = clientValue;

            // hook should change it on client
            int baseCallCount = 0;
            clientObject.BaseHookCalled += (oldValue, newValue) =>
            {
                baseCallCount++;
                Assert.That(oldValue, Is.EqualTo(clientValue));
                Assert.That(newValue, Is.EqualTo(serverValue));
            };

            ProcessMessages();
            Assert.That(baseCallCount, Is.EqualTo(1));
        }

        [Test]
        public void VirtualOverrideHook_HookCalledWhenSyncingChangedValued()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out VirtualOverrideHook serverObject,
                out _, out _, out VirtualOverrideHook clientObject);

            const int serverValue = 24;

            // change it on server
            serverObject.value = serverValue;

            // hook should change it on client
            int overrideCallCount = 0;
            int baseCallCount = 0;
            clientObject.OverrideHookCalled += (oldValue, newValue) =>
            {
                overrideCallCount++;
                Assert.That(oldValue, Is.EqualTo(0));
                Assert.That(newValue, Is.EqualTo(serverValue));
            };
            clientObject.BaseHookCalled += (oldValue, newValue) =>
            {
                baseCallCount++;
            };

            ProcessMessages();
            Assert.That(overrideCallCount, Is.EqualTo(1));
            Assert.That(baseCallCount, Is.EqualTo(0));
        }

        [Test]
        public void AbstractHook_HookCalledWhenSyncingChangedValued()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out AbstractHook serverObject,
                out _, out _, out AbstractHook clientObject);

            const int clientValue = 10;
            const int serverValue = 24;

            // change it on server
            serverObject.value = serverValue;
            clientObject.value = clientValue;

            // hook should change it on client
            int callCount = 0;
            clientObject.HookCalled += (oldValue, newValue) =>
            {
                callCount++;
                Assert.That(oldValue, Is.EqualTo(clientValue));
                Assert.That(newValue, Is.EqualTo(serverValue));
            };

            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        // test to prevent the SyncVar<T> Weaver bug that imer found.
        // https://github.com/vis2k/Mirror/pull/2957#issuecomment-1019692366
        // when loading "n = MySyncVar.n", 'ldfdla' loads 'MySyncVar'.
        // if we replace MySyncVar with a weaved version like for SyncVar<T>,
        // then ldflda for cases like "n = MySyncVar.n" needs to be replaced too.
        //
        // this wasn't necessary for the original SyncVars, which is why the bug
        // wasn't caught by a unit test to begin with.
        [Test]
        public void ImerHook_Ldflda_Uses_Correct_SyncVard()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out ImerHook_Ldflda serverObject,
                out _, out _, out ImerHook_Ldflda clientObject);

            // change it on server
            serverObject._syncProportions = new Proportions{Array = new byte[]{3, 4}};

            // sync to client
            ProcessMessages();

            // client uses ldflda to get replacement.Array.
            // we synced an array with two values, so if ldflda uses the correct
            // SyncVar then it shouldn't be null anymore now.
            Assert.That(clientObject.ldflda_Array, !Is.Null);
        }

        // repro for the bug found by David_548219 in discord where setting
        // MyStruct.value would throw invalid IL.
        // could happen if we change Weaver [SyncVar] logic / replacements.
        // testing syncVar = X isn't enough.
        // we should have a test for syncVar.value = X too.
        [Test]
        public void DavidHook_SetSyncVarStructsValued()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out DavidHookComponent serverObject,
                out _, out _, out DavidHookComponent clientObject);

            // change it on server.
            // should not throw.
            serverObject.syncvar.Value = 1337;
        }
    }
}
