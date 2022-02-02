using System;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.RemoteAttrributeTest
{
    class RpcNetworkIdentityBehaviour : NetworkBehaviour
    {
        public event Action<NetworkIdentity> onSendNetworkIdentityCalled;
        public event Action<GameObject> onSendGameObjectCalled;
        public event Action<NetworkBehaviour> onSendNetworkBehaviourCalled;
        public event Action<RpcNetworkIdentityBehaviour> onSendNetworkBehaviourDerivedCalled;

        [ClientRpc]
        public void SendNetworkIdentity(NetworkIdentity value)
        {
            onSendNetworkIdentityCalled?.Invoke(value);
        }

        [ClientRpc]
        public void SendGameObject(GameObject value)
        {
            onSendGameObjectCalled?.Invoke(value);
        }

        [ClientRpc]
        public void SendNetworkBehaviour(NetworkBehaviour value)
        {
            onSendNetworkBehaviourCalled?.Invoke(value);
        }

        [ClientRpc]
        public void SendNetworkBehaviourDerived(RpcNetworkIdentityBehaviour value)
        {
            onSendNetworkBehaviourDerivedCalled?.Invoke(value);
        }
    }

    [Description("Test for sending NetworkIdentity fields (NI/GO/NB) in RPC")]
    public class RpcNetworkIdentityTest : MirrorTest
    {
        NetworkConnectionToClient connectionToClient;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            // start server/client
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out connectionToClient);
        }

        [TearDown]
        public override void TearDown() => base.TearDown();

        [Test]
        public void RpcCanSendNetworkIdentity()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out RpcNetworkIdentityBehaviour serverOwnerComponent,
                                    out _, out _, out RpcNetworkIdentityBehaviour clientOwnerComponent,
                                    connectionToClient);
            CreateNetworkedAndSpawn(out _, out NetworkIdentity serverExpected, out RpcNetworkIdentityBehaviour _,
                                    out _, out NetworkIdentity clientExpected, out _,
                                    connectionToClient);

            int callCount = 0;
            clientOwnerComponent.onSendNetworkIdentityCalled += actual =>
            {
                callCount++;
                // Utils.GetSpawnedInServerOrClient finds the server one before the client one
                Assert.That(actual, Is.EqualTo(serverExpected));
                //Assert.That(actual, Is.EqualTo(clientExpected));
            };
            serverOwnerComponent.SendNetworkIdentity(serverExpected);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void RpcCanSendGameObject()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out RpcNetworkIdentityBehaviour serverOwnerComponent,
                                    out _, out _, out RpcNetworkIdentityBehaviour clientOwnerComponent,
                                    connectionToClient);
            CreateNetworkedAndSpawn(out GameObject serverExpected, out _, out RpcNetworkIdentityBehaviour _,
                                    out GameObject clientExpected, out _, out _,
                                    connectionToClient);

            serverOwnerComponent.name = nameof(serverOwnerComponent);
            clientOwnerComponent.name = nameof(clientOwnerComponent);
            serverExpected.name = nameof(serverExpected);
            clientExpected.name = nameof(clientExpected);

            int callCount = 0;
            clientOwnerComponent.onSendGameObjectCalled += actual =>
            {
                callCount++;
                // Utils.GetSpawnedInServerOrClient finds the server one before the client one
                Assert.That(actual, Is.EqualTo(serverExpected));
                //Assert.That(actual, Is.EqualTo(clientExpected));
            };
            serverOwnerComponent.SendGameObject(serverExpected);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void RpcCanSendNetworkBehaviour()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out RpcNetworkIdentityBehaviour serverOwnerComponent,
                                    out _, out _, out RpcNetworkIdentityBehaviour clientOwnerComponent,
                                    connectionToClient);
            CreateNetworkedAndSpawn(out _, out _, out RpcNetworkIdentityBehaviour serverExpected,
                                    out _, out _, out RpcNetworkIdentityBehaviour clientExpected,
                                    connectionToClient);

            int callCount = 0;
            clientOwnerComponent.onSendNetworkBehaviourCalled += actual =>
            {
                callCount++;
                // Utils.GetSpawnedInServerOrClient finds the server one before the client one
                Assert.That(actual, Is.EqualTo(serverExpected));
                //Assert.That(actual, Is.EqualTo(clientExpected));
            };
            serverOwnerComponent.SendNetworkBehaviour(serverExpected);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void RpcCanSendNetworkBehaviourDerived()
        {
            // spawn with owner
            CreateNetworkedAndSpawn(out _, out _, out RpcNetworkIdentityBehaviour serverOwnerComponent,
                                    out _, out _, out RpcNetworkIdentityBehaviour clientOwnerComponent,
                                    connectionToClient);
            CreateNetworkedAndSpawn(out _, out _, out RpcNetworkIdentityBehaviour serverExpected,
                                    out _, out _, out RpcNetworkIdentityBehaviour clientExpected,
                                    connectionToClient);

            int callCount = 0;
            clientOwnerComponent.onSendNetworkBehaviourDerivedCalled += actual =>
             {
                 callCount++;
                // Utils.GetSpawnedInServerOrClient finds the server one before the client one
                Assert.That(actual, Is.EqualTo(serverExpected));
                //Assert.That(actual, Is.EqualTo(clientExpected));
             };
            serverOwnerComponent.SendNetworkBehaviourDerived(serverExpected);
            ProcessMessages();
            Assert.That(callCount, Is.EqualTo(1));
        }
    }
}
