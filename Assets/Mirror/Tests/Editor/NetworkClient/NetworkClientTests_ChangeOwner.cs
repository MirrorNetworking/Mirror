using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.NetworkClients
{
    public class NetworkClientTests_ChangeOwner : MirrorEditModeTest
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            NetworkServer.Listen(1);
            ConnectClientBlocking(out _);
        }

        // ?? ChangeOwner ???????????????????????????????????????????????????????

        [Test]
        public void ChangeOwner_SetsIsOwnedTrue()
        {
            CreateNetworked(out _, out NetworkIdentity identity);
            NetworkClient.ChangeOwner(identity, new ChangeOwnerMessage { isOwner = true });
            Assert.That(identity.isOwned, Is.True);
        }

        [Test]
        public void ChangeOwner_SetsIsOwnedFalse()
        {
            CreateNetworked(out _, out NetworkIdentity identity);
            identity.isOwned = true;
            NetworkClient.ChangeOwner(identity, new ChangeOwnerMessage { isOwner = false });
            Assert.That(identity.isOwned, Is.False);
        }

        [Test]
        public void ChangeOwner_AddsToConnectionOwnedWhenGranted()
        {
            CreateNetworked(out _, out NetworkIdentity identity);
            NetworkClient.ChangeOwner(identity, new ChangeOwnerMessage { isOwner = true });
            Assert.That(NetworkClient.connection.owned.Contains(identity), Is.True);
        }

        [Test]
        public void ChangeOwner_RemovesFromConnectionOwnedWhenRevoked()
        {
            CreateNetworked(out _, out NetworkIdentity identity);
            NetworkClient.connection.owned.Add(identity);
            NetworkClient.ChangeOwner(identity, new ChangeOwnerMessage { isOwner = false });
            Assert.That(NetworkClient.connection.owned.Contains(identity), Is.False);
        }

        [Test]
        public void ChangeOwner_SetsLocalPlayerWhenGranted()
        {
            CreateNetworked(out _, out NetworkIdentity identity);
            NetworkClient.ChangeOwner(identity, new ChangeOwnerMessage { isOwner = true, isLocalPlayer = true });
            Assert.That(NetworkClient.localPlayer, Is.EqualTo(identity));
            Assert.That(identity.isLocalPlayer, Is.True);
        }

        [Test]
        public void ChangeOwner_ClearsLocalPlayerWhenRevoked()
        {
            CreateNetworked(out _, out NetworkIdentity identity);
            // make it the local player first
            NetworkClient.localPlayer = identity;
            identity.isLocalPlayer = true;
            NetworkClient.ChangeOwner(identity, new ChangeOwnerMessage { isOwner = false, isLocalPlayer = false });
            Assert.That(NetworkClient.localPlayer, Is.Null);
            Assert.That(identity.isLocalPlayer, Is.False);
        }

        [Test]
        public void ChangeOwner_DoesNotClearLocalPlayerWhenDifferentIdentityRevoked()
        {
            CreateNetworked(out _, out NetworkIdentity localPlayerIdentity);
            CreateNetworked(out _, out NetworkIdentity otherIdentity);

            // localPlayerIdentity is the local player; revoke ownership on a different object
            NetworkClient.localPlayer = localPlayerIdentity;
            NetworkClient.ChangeOwner(otherIdentity, new ChangeOwnerMessage { isOwner = false, isLocalPlayer = false });

            // localPlayer should remain unchanged
            Assert.That(NetworkClient.localPlayer, Is.EqualTo(localPlayerIdentity));
        }

        // ?? OnChangeOwner ?????????????????????????????????????????????????????

        [Test]
        public void OnChangeOwner_LogsErrorForUnknownNetId()
        {
            LogAssert.Expect(LogType.Error, "OnChangeOwner: Could not find object with netId 9999");
            NetworkClient.OnChangeOwner(new ChangeOwnerMessage { netId = 9999 });
        }

        [Test]
        public void OnChangeOwner_AppliesOwnershipToKnownObject()
        {
            CreateNetworked(out _, out NetworkIdentity identity);
            const uint netId = 500;
            identity.netId = netId;
            NetworkClient.spawned[netId] = identity;

            NetworkClient.OnChangeOwner(new ChangeOwnerMessage { netId = netId, isOwner = true });

            Assert.That(identity.isOwned, Is.True);

            // cleanup
            NetworkClient.spawned.Remove(netId);
        }

        // Spy used only by ChangeOwner_CallsOnStopLocalPlayerWhenLocalPlayerRevoked.
        class StopLocalPlayerSpy : NetworkBehaviour
        {
            public bool called = false;
            public override void OnStopLocalPlayer() => called = true;
        }

        [Test]
        public void ChangeOwner_CallsOnStopLocalPlayerWhenLocalPlayerRevoked()
        {
            CreateNetworked(out _, out NetworkIdentity identity, out StopLocalPlayerSpy spy);
            identity.isLocalPlayer = true;

            NetworkClient.ChangeOwner(identity, new ChangeOwnerMessage
            {
                isOwner = false,
                isLocalPlayer = false
            });

            Assert.That(spy.called, Is.True);
        }
    }
}