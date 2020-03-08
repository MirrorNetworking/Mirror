using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using Mirror.Tcp;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    class EmptyBehaviour : NetworkBehaviour
    {
    }

    // we need to inherit from networkbehaviour to test protected functions
    public class NetworkBehaviourDelegateComponent : NetworkBehaviour
    {
        public static void Delegate(NetworkBehaviour comp, NetworkReader reader) {}
        public static void Delegate2(NetworkBehaviour comp, NetworkReader reader) {}
    }

    public class NetworkBehaviourTests : HostTests
    {
        #region Setup
        GameObject playerGO;

        EmptyBehaviour behavior;
        NetworkIdentity identity;

        [SetUp]
        public void SetupNetworkServer()
        {
            SetupHost();

            playerGO = new GameObject();
            identity = playerGO.AddComponent<NetworkIdentity>();
            behavior = playerGO.AddComponent<EmptyBehaviour>();

            manager.server.AddPlayerForConnection(manager.server.localConnection, playerGO);

        }

        [TearDown]
        public void ShutdownNetworkServer()
        {
            GameObject.DestroyImmediate(playerGO);

            ShutdownHost();
        }

        #endregion

        [Test]
        public void IsServerOnly()
        {
            Assert.That(behavior.isServer, Is.True);
            Assert.That(behavior.isServerOnly, Is.False);
       }

        [Test]
        public void IsClient()
        {
            Assert.That(behavior.isClient, Is.True);
        }

        [Test]
        public void PlayerHasAuthorityByDefault()
        {
            // no authority by default
            Assert.That(behavior.hasAuthority, Is.True);
        }

        [UnityTest]
        public IEnumerator SpawnedObjectNoAuthority()
        {
            var gameObject2 = new GameObject();
            gameObject2.AddComponent<NetworkIdentity>();
            EmptyBehaviour behaviour2 = gameObject2.AddComponent<EmptyBehaviour>();

            server.Spawn(gameObject2);

            yield return null;

            // no authority by default
            Assert.That(behaviour2.hasAuthority, Is.False);
        }

        [Test]
        public void HasIdentitysNetId()
        {
            identity.netId = 42;
            Assert.That(behavior.netId, Is.EqualTo(42));
        }

        [Test]
        public void HasIdentitysConnectionToServer()
        {
            identity.connectionToServer = new ULocalConnectionToServer();
            Assert.That(behavior.connectionToServer, Is.EqualTo(identity.connectionToServer));
        }

        [Test]
        public void HasIdentitysConnectionToClient()
        {
            identity.connectionToClient = new ULocalConnectionToClient();
            Assert.That(behavior.connectionToClient, Is.EqualTo(identity.connectionToClient));
        }

        [Test]
        public void ComponentIndex()
        {
            var extraObject = new GameObject();

            extraObject.AddComponent<NetworkIdentity>();

            EmptyBehaviour behaviour1 = extraObject.AddComponent<EmptyBehaviour>();
            EmptyBehaviour behaviour2 = extraObject.AddComponent<EmptyBehaviour>();

            // original one is first networkbehaviour, so index is 0
            Assert.That(behaviour1.ComponentIndex, Is.EqualTo(0));
            // extra one is second networkbehaviour, so index is 1
            Assert.That(behaviour2.ComponentIndex, Is.EqualTo(1));

            GameObject.DestroyImmediate(extraObject);
        }

        [Test]
        public void OnCheckObserverTrueByDefault()
        {
            Assert.That(behavior.OnCheckObserver(null), Is.True);
        }

        [Test]
        public void RegisterDelegateDoesntOverwrite()
        {
            // registerdelegate is protected, but we can use
            // RegisterCommandDelegate which calls RegisterDelegate
            NetworkBehaviour.RegisterCommandDelegate(
                typeof(NetworkBehaviourDelegateComponent),
                nameof(NetworkBehaviourDelegateComponent.Delegate),
                NetworkBehaviourDelegateComponent.Delegate);

            // registering the exact same one should be fine. it should simply
            // do nothing.
            NetworkBehaviour.RegisterCommandDelegate(
                typeof(NetworkBehaviourDelegateComponent),
                nameof(NetworkBehaviourDelegateComponent.Delegate),
                NetworkBehaviourDelegateComponent.Delegate);

            // registering the same name with a different callback shouldn't
            // work
            LogAssert.Expect(LogType.Error, "Function " + typeof(NetworkBehaviourDelegateComponent) + "." + nameof(NetworkBehaviourDelegateComponent.Delegate) + " and " + typeof(NetworkBehaviourDelegateComponent) + "." + nameof(NetworkBehaviourDelegateComponent.Delegate2) + " have the same hash.  Please rename one of them");
            NetworkBehaviour.RegisterCommandDelegate(
                typeof(NetworkBehaviourDelegateComponent),
                nameof(NetworkBehaviourDelegateComponent.Delegate),
                NetworkBehaviourDelegateComponent.Delegate2);

            // clean up
            NetworkBehaviour.ClearDelegates();
        }

        [Test]
        public void GetDelegate()
        {
            // registerdelegate is protected, but we can use
            // RegisterCommandDelegate which calls RegisterDelegate
            NetworkBehaviour.RegisterCommandDelegate(
                typeof(NetworkBehaviourDelegateComponent),
                nameof(NetworkBehaviourDelegateComponent.Delegate),
                NetworkBehaviourDelegateComponent.Delegate);

            // get handler
            int cmdHash = NetworkBehaviour.GetMethodHash(typeof(NetworkBehaviourDelegateComponent), nameof(NetworkBehaviourDelegateComponent.Delegate));
            NetworkBehaviour.CmdDelegate func = NetworkBehaviour.GetDelegate(cmdHash);
            NetworkBehaviour.CmdDelegate expected = NetworkBehaviourDelegateComponent.Delegate;
            Assert.That(func, Is.EqualTo(expected));

            // invalid hash should return null handler
            NetworkBehaviour.CmdDelegate funcNull = NetworkBehaviour.GetDelegate(1234);
            Assert.That(funcNull, Is.Null);

            // clean up
            NetworkBehaviour.ClearDelegates();
        }

        // NOTE: SyncVarGameObjectEqual should be static later
        [Test]
        public void SyncVarGameObjectEqualZeroNetIdNullIsTrue()
        {
            // null and identity.netid==0 returns true (=equal)
            //
            // later we should reevaluate if this is so smart or not. might be
            // better to return false here.
            // => we possibly return false so that resync doesn't happen when
            //    GO disappears? or not?
            bool result = behavior.SyncVarGameObjectEqual(null, 0);
            Assert.That(result, Is.True);
        }

        // NOTE: SyncVarGameObjectEqual should be static later
        [Test]
        public void SyncVarGameObjectEqualNull()
        {
            // our identity should have a netid for comparing
            identity.netId = 42;

            // null should return false
            bool result = behavior.SyncVarGameObjectEqual(null, identity.netId);
            Assert.That(result, Is.False);
        }

        // NOTE: SyncVarGameObjectEqual should be static later
        [Test]
        public void SyncVarGameObjectEqualZeroNetIdAndGOWithoutIdentityComponentIsTrue()
        {
            // null and identity.netid==0 returns true (=equal)
            //
            // later we should reevaluate if this is so smart or not. might be
            // better to return false here.
            // => we possibly return false so that resync doesn't happen when
            //    GO disappears? or not?
            GameObject go = new GameObject();
            bool result = behavior.SyncVarGameObjectEqual(go, 0);
            Assert.That(result, Is.True);
        }

        // NOTE: SyncVarGameObjectEqual should be static later
        [Test]
        public void SyncVarGameObjectEqualWithoutIdentityComponent()
        {
            // our identity should have a netid for comparing
            identity.netId = 42;

            // gameobject without networkidentity component should return false
            GameObject go = new GameObject();
            bool result = behavior.SyncVarGameObjectEqual(go, identity.netId);
            Assert.That(result, Is.False);

            // clean up
            GameObject.DestroyImmediate(go);
        }

        // NOTE: SyncVarGameObjectEqual should be static later
        [Test]
        public void SyncVarGameObjectEqualValidGOWithDifferentNetId()
        {
            // our identity should have a netid for comparing
            identity.netId = 42;

            // gameobject with valid networkidentity and netid that is different
            GameObject go = new GameObject();
            NetworkIdentity ni = go.AddComponent<NetworkIdentity>();
            ni.netId = 43;
            bool result = behavior.SyncVarGameObjectEqual(go, identity.netId);
            Assert.That(result, Is.False);

            // clean up
            GameObject.DestroyImmediate(go);
        }

        // NOTE: SyncVarGameObjectEqual should be static later
        [Test]
        public void SyncVarGameObjectEqualValidGOWithSameNetId()
        {
            // our identity should have a netid for comparing
            identity.netId = 42;

            // gameobject with valid networkidentity and netid that is different
            GameObject go = new GameObject();
            NetworkIdentity ni = go.AddComponent<NetworkIdentity>();
            ni.netId = 42;
            bool result = behavior.SyncVarGameObjectEqual(go, identity.netId);
            Assert.That(result, Is.True);

            // clean up
            GameObject.DestroyImmediate(go);
        }

        // NOTE: SyncVarGameObjectEqual should be static later
        [Test]
        public void SyncVarGameObjectEqualUnspawnedGO()
        {
            // our identity should have a netid for comparing
            identity.netId = 42;

            // gameobject with valid networkidentity and 0 netid that is unspawned
            var go = new GameObject();
            go.AddComponent<NetworkIdentity>();
            LogAssert.Expect(LogType.Warning, "SetSyncVarGameObject GameObject " + go + " has a zero netId. Maybe it is not spawned yet?");
            bool result = behavior.SyncVarGameObjectEqual(go, identity.netId);
            Assert.That(result, Is.False);

            // clean up
            GameObject.DestroyImmediate(go);
        }

        // NOTE: SyncVarGameObjectEqual should be static later
        [Test]
        public void SyncVarGameObjectEqualUnspawnedGOZeroNetIdIsTrue()
        {
            // unspawned go and identity.netid==0 returns true (=equal)
            var go = new GameObject();
            go.AddComponent<NetworkIdentity>();
            LogAssert.Expect(LogType.Warning, "SetSyncVarGameObject GameObject " + go + " has a zero netId. Maybe it is not spawned yet?");
            bool result = behavior.SyncVarGameObjectEqual(go, 0);
            Assert.That(result, Is.True);

            // clean up
            GameObject.DestroyImmediate(go);
        }

        // NOTE: SyncVarNetworkIdentityEqual should be static later
        [Test]
        public void SyncVarNetworkIdentityEqualZeroNetIdNullIsTrue()
        {
            // null and identity.netid==0 returns true (=equal)
            //
            // later we should reevaluate if this is so smart or not. might be
            // better to return false here.
            // => we possibly return false so that resync doesn't happen when
            //    GO disappears? or not?
            bool result = behavior.SyncVarNetworkIdentityEqual(null, 0);
            Assert.That(result, Is.True);
        }

        // NOTE: SyncVarNetworkIdentityEqual should be static later
        [Test]
        public void SyncVarNetworkIdentityEqualNull()
        {
            // our identity should have a netid for comparing
            identity.netId = 42;

            // null should return false
            bool result = behavior.SyncVarNetworkIdentityEqual(null, identity.netId);
            Assert.That(result, Is.False);
        }

        // NOTE: SyncVarNetworkIdentityEqual should be static later
        [Test]
        public void SyncVarNetworkIdentityEqualValidIdentityWithDifferentNetId()
        {
            // our identity should have a netid for comparing
            identity.netId = 42;

            // gameobject with valid networkidentity and netid that is different
            GameObject go = new GameObject();
            NetworkIdentity ni = go.AddComponent<NetworkIdentity>();
            ni.netId = 43;
            bool result = behavior.SyncVarNetworkIdentityEqual(ni, identity.netId);
            Assert.That(result, Is.False);

            // clean up
            GameObject.DestroyImmediate(go);
        }

        // NOTE: SyncVarNetworkIdentityEqual should be static later
        [Test]
        public void SyncVarNetworkIdentityEqualValidIdentityWithSameNetId()
        {
            // our identity should have a netid for comparing
            identity.netId = 42;

            // gameobject with valid networkidentity and netid that is different
            var go = new GameObject();
            NetworkIdentity ni = go.AddComponent<NetworkIdentity>();
            ni.netId = 42;
            bool result = behavior.SyncVarNetworkIdentityEqual(ni, identity.netId);
            Assert.That(result, Is.True);

            // clean up
            GameObject.DestroyImmediate(go);
        }

        // NOTE: SyncVarNetworkIdentityEqual should be static later
        [Test]
        public void SyncVarNetworkIdentityEqualUnspawnedIdentity()
        {
            // our identity should have a netid for comparing
            identity.netId = 42;

            // gameobject with valid networkidentity and 0 netid that is unspawned
            var go = new GameObject();
            NetworkIdentity ni = go.AddComponent<NetworkIdentity>();
            LogAssert.Expect(LogType.Warning, "SetSyncVarNetworkIdentity NetworkIdentity " + ni + " has a zero netId. Maybe it is not spawned yet?");
            bool result = behavior.SyncVarNetworkIdentityEqual(ni, identity.netId);
            Assert.That(result, Is.False);

            // clean up
            GameObject.DestroyImmediate(go);
        }

        // NOTE: SyncVarNetworkIdentityEqual should be static later
        [Test]
        public void SyncVarNetworkIdentityEqualUnspawnedIdentityZeroNetIdIsTrue()
        {
            // unspawned go and identity.netid==0 returns true (=equal)
            var go = new GameObject();
            NetworkIdentity ni = go.AddComponent<NetworkIdentity>();
            LogAssert.Expect(LogType.Warning, "SetSyncVarNetworkIdentity NetworkIdentity " + ni + " has a zero netId. Maybe it is not spawned yet?");
            bool result = behavior.SyncVarNetworkIdentityEqual(ni, 0);
            Assert.That(result, Is.True);

            // clean up
            GameObject.DestroyImmediate(go);
        }
    }

    // we need to inherit from networkbehaviour to test protected functions
    public class NetworkBehaviourHookGuardTester : NetworkBehaviour
    {
        [Test]
        public void HookGuard()
        {
            // set hook guard for some bits
            for (int i = 0; i < 10; ++i)
            {
                ulong bit = 1ul << i;

                // should be false by default
                Assert.That(getSyncVarHookGuard(bit), Is.False);

                // set true
                setSyncVarHookGuard(bit, true);
                Assert.That(getSyncVarHookGuard(bit), Is.True);

                // set false again
                setSyncVarHookGuard(bit, false);
                Assert.That(getSyncVarHookGuard(bit), Is.False);
            }
        }
    }

    // we need to inherit from networkbehaviour to test protected functions
    public class NetworkBehaviourInitSyncObjectTester : NetworkBehaviour
    {
        [Test]
        public void InitSyncObject()
        {
            ISyncObject syncObject = new SyncListBool();
            InitSyncObject(syncObject);
            Assert.That(syncObjects.Count, Is.EqualTo(1));
            Assert.That(syncObjects[0], Is.EqualTo(syncObject));
        }
    }
}
