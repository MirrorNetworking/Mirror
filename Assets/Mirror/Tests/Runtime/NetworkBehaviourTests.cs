using NUnit.Framework;
using UnityEngine;

using static Mirror.Tests.LocalConnections;

namespace Mirror.Tests
{
    public class SampleBehavior : NetworkBehaviour
    {
        public bool SyncVarGameObjectEqualExposed(GameObject newGameObject, uint netIdField)
        {
            return SyncVarGameObjectEqual(newGameObject, netIdField);
        }

        public bool SyncVarNetworkIdentityEqualExposed(NetworkIdentity ni, uint netIdField)
        {
            return SyncVarNetworkIdentityEqual(ni, netIdField);
        }

    }

    // we need to inherit from networkbehaviour to test protected functions
    public class NetworkBehaviourDelegateComponent : NetworkBehaviour
    {
        public static void Delegate(NetworkBehaviour comp, NetworkReader reader) { }
        public static void Delegate2(NetworkBehaviour comp, NetworkReader reader) { }
    }

    public class NetworkBehaviourTests : HostSetup<SampleBehavior>
    {
        #region Component flags
        [Test]
        public void IsServerOnly()
        {
            Assert.That(component.IsServerOnly, Is.False);
        }

        [Test]
        public void IsServer()
        {
            Assert.That(component.IsServer, Is.True);
        }

        [Test]
        public void IsClient()
        {
            Assert.That(component.IsClient, Is.True);
        }

        [Test]
        public void IsClientOnly()
        {
            Assert.That(component.IsClientOnly, Is.False);
        }

        [Test]
        public void PlayerHasAuthorityByDefault()
        {
            // no authority by default
            Assert.That(component.HasAuthority, Is.True);
        }

        #endregion

        class OnStartServerTestComponent : NetworkBehaviour
        {
            public bool called;

            public void OnStartServer()
            {
                Assert.That(IsClient, Is.True);
                Assert.That(IsLocalPlayer, Is.False);
                Assert.That(IsServer, Is.True);
                called = true;
            }
        };

        // check isClient/isServer/isLocalPlayer in server-only mode
        [Test]
        public void OnStartServer()
        {
            var gameObject = new GameObject();
            NetworkIdentity netIdentity = gameObject.AddComponent<NetworkIdentity>();
            OnStartServerTestComponent comp = gameObject.AddComponent<OnStartServerTestComponent>();
            netIdentity.OnStartServer.AddListener(comp.OnStartServer);

            Assert.That(comp.called, Is.False);

            server.Spawn(gameObject);

            netIdentity.StartServer();

            Assert.That(comp.called, Is.True);

            GameObject.DestroyImmediate(gameObject);
        }


        [Test]
        public void SpawnedObjectNoAuthority()
        {
            var gameObject2 = new GameObject();
            gameObject2.AddComponent<NetworkIdentity>();
            SampleBehavior behaviour2 = gameObject2.AddComponent<SampleBehavior>();

            server.Spawn(gameObject2);

            client.Update();

            // no authority by default
            Assert.That(behaviour2.HasAuthority, Is.False);
        }

        [Test]
        public void HasIdentitysNetId()
        {
            identity.NetId = 42;
            Assert.That(component.NetId, Is.EqualTo(42));
        }

        [Test]
        public void TimeTest()
        {
            SampleBehavior behaviour1 = playerGO.AddComponent<SampleBehavior>();
            Assert.That(behaviour1.NetworkTime, Is.EqualTo(client.Time));
        }

        [Test]
        public void HasIdentitysConnectionToServer()
        {
            (identity.ConnectionToServer, _) = PipedConnections();
            Assert.That(component.ConnectionToServer, Is.EqualTo(identity.ConnectionToServer));
        }

        [Test]
        public void HasIdentitysConnectionToClient()
        {
            (_, identity.ConnectionToClient) = PipedConnections();
            Assert.That(component.ConnectionToClient, Is.EqualTo(identity.ConnectionToClient));
        }

        [Test]
        public void ComponentIndex()
        {
            var extraObject = new GameObject();

            extraObject.AddComponent<NetworkIdentity>();

            SampleBehavior behaviour1 = extraObject.AddComponent<SampleBehavior>();
            SampleBehavior behaviour2 = extraObject.AddComponent<SampleBehavior>();

            // original one is first networkbehaviour, so index is 0
            Assert.That(behaviour1.ComponentIndex, Is.EqualTo(0));
            // extra one is second networkbehaviour, so index is 1
            Assert.That(behaviour2.ComponentIndex, Is.EqualTo(1));

            GameObject.DestroyImmediate(extraObject);
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
            bool result = component.SyncVarGameObjectEqualExposed(null, 0);
            Assert.That(result, Is.True);
        }

        // NOTE: SyncVarGameObjectEqual should be static later
        [Test]
        public void SyncVarGameObjectEqualNull()
        {
            // our identity should have a netid for comparing
            identity.NetId = 42;

            // null should return false
            bool result = component.SyncVarGameObjectEqualExposed(null, identity.NetId);
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
            var go = new GameObject();
            bool result = component.SyncVarGameObjectEqualExposed(go, 0);
            Assert.That(result, Is.True);
        }

        // NOTE: SyncVarGameObjectEqual should be static later
        [Test]
        public void SyncVarGameObjectEqualWithoutIdentityComponent()
        {
            // our identity should have a netid for comparing
            identity.NetId = 42;

            // gameobject without networkidentity component should return false
            var go = new GameObject();
            bool result = component.SyncVarGameObjectEqualExposed(go, identity.NetId);
            Assert.That(result, Is.False);

            // clean up
            GameObject.DestroyImmediate(go);
        }

        // NOTE: SyncVarGameObjectEqual should be static later
        [Test]
        public void SyncVarGameObjectEqualValidGOWithDifferentNetId()
        {
            // our identity should have a netid for comparing
            identity.NetId = 42;

            // gameobject with valid networkidentity and netid that is different
            var go = new GameObject();
            NetworkIdentity ni = go.AddComponent<NetworkIdentity>();
            ni.NetId = 43;
            bool result = component.SyncVarGameObjectEqualExposed(go, identity.NetId);
            Assert.That(result, Is.False);

            // clean up
            GameObject.DestroyImmediate(go);
        }

        // NOTE: SyncVarGameObjectEqual should be static later
        [Test]
        public void SyncVarGameObjectEqualValidGOWithSameNetId()
        {
            // our identity should have a netid for comparing
            identity.NetId = 42;

            // gameobject with valid networkidentity and netid that is different
            var go = new GameObject();
            NetworkIdentity ni = go.AddComponent<NetworkIdentity>();
            ni.NetId = 42;
            bool result = component.SyncVarGameObjectEqualExposed(go, identity.NetId);
            Assert.That(result, Is.True);

            // clean up
            GameObject.DestroyImmediate(go);
        }

        // NOTE: SyncVarGameObjectEqual should be static later
        [Test]
        public void SyncVarGameObjectEqualUnspawnedGO()
        {
            // our identity should have a netid for comparing
            identity.NetId = 42;

            // gameobject with valid networkidentity and 0 netid that is unspawned
            var go = new GameObject();
            go.AddComponent<NetworkIdentity>();
            bool result = component.SyncVarGameObjectEqualExposed(go, identity.NetId);
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
            bool result = component.SyncVarGameObjectEqualExposed(go, 0);
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
            bool result = component.SyncVarGameObjectEqualExposed(null, 0);
            Assert.That(result, Is.True);
        }

        // NOTE: SyncVarNetworkIdentityEqual should be static later
        [Test]
        public void SyncVarNetworkIdentityEqualNull()
        {
            // our identity should have a netid for comparing
            identity.NetId = 42;

            // null should return false
            bool result = component.SyncVarGameObjectEqualExposed(null, identity.NetId);
            Assert.That(result, Is.False);
        }

        // NOTE: SyncVarNetworkIdentityEqual should be static later
        [Test]
        public void SyncVarNetworkIdentityEqualValidIdentityWithDifferentNetId()
        {
            // our identity should have a netid for comparing
            identity.NetId = 42;

            // gameobject with valid networkidentity and netid that is different
            var go = new GameObject();
            NetworkIdentity ni = go.AddComponent<NetworkIdentity>();
            ni.NetId = 43;
            bool result = component.SyncVarNetworkIdentityEqualExposed(ni, identity.NetId);
            Assert.That(result, Is.False);

            // clean up
            GameObject.DestroyImmediate(go);
        }

        // NOTE: SyncVarNetworkIdentityEqual should be static later
        [Test]
        public void SyncVarNetworkIdentityEqualValidIdentityWithSameNetId()
        {
            // our identity should have a netid for comparing
            identity.NetId = 42;

            // gameobject with valid networkidentity and netid that is different
            var go = new GameObject();
            NetworkIdentity ni = go.AddComponent<NetworkIdentity>();
            ni.NetId = 42;
            bool result = component.SyncVarNetworkIdentityEqualExposed(ni, identity.NetId);
            Assert.That(result, Is.True);

            // clean up
            GameObject.DestroyImmediate(go);
        }

        // NOTE: SyncVarNetworkIdentityEqual should be static later
        [Test]
        public void SyncVarNetworkIdentityEqualUnspawnedIdentity()
        {
            // our identity should have a netid for comparing
            identity.NetId = 42;

            // gameobject with valid networkidentity and 0 netid that is unspawned
            var go = new GameObject();
            NetworkIdentity ni = go.AddComponent<NetworkIdentity>();
            bool result = component.SyncVarNetworkIdentityEqualExposed(ni, identity.NetId);
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
            bool result = component.SyncVarNetworkIdentityEqualExposed(ni, 0);
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
                Assert.That(GetSyncVarHookGuard(bit), Is.False);

                // set true
                SetSyncVarHookGuard(bit, true);
                Assert.That(GetSyncVarHookGuard(bit), Is.True);

                // set false again
                SetSyncVarHookGuard(bit, false);
                Assert.That(GetSyncVarHookGuard(bit), Is.False);
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
