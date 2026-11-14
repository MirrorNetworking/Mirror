using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.NetworkClients
{
    public class NetworkClientTests_HostMode : MirrorEditModeTest
    {
        // Minimal spy to track SetHostVisibility calls without touching renderers.
        class InterestManagementSpy : InterestManagementBase
        {
            public NetworkIdentity lastIdentity;
            public bool lastVisible = true; // default true so a false call is distinguishable

            public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnectionToClient newObserver) => true;
            public override void Rebuild(NetworkIdentity identity, bool initialize) { }

            // No [ServerCallback] — override runs unconditionally without a server-active guard.
            public override void SetHostVisibility(NetworkIdentity identity, bool visible)
            {
                lastIdentity = identity;
                lastVisible  = visible;
            }
        }

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            NetworkServer.Listen(1);
        }

        [TearDown]
        public override void TearDown()
        {
            // InterestManagementBase.OnEnable sets both — clear before base tears down.
            NetworkClient.aoi = null;
            NetworkServer.aoi = null;
            base.TearDown();
        }

        // Serialize an ObjectHideMessage body (no message-id prefix) and dispatch
        // it directly through NetworkClient's registered handler dictionary.
        void InvokeObjectHideHandler(uint netId)
        {
            ushort msgType = NetworkMessageId<ObjectHideMessage>.Id;
            using NetworkWriterPooled writer = NetworkWriterPool.Get();
            writer.Write(new ObjectHideMessage { netId = netId });
            using NetworkReaderPooled reader = NetworkReaderPool.Get(writer.ToArraySegment());
            NetworkClient.handlers[msgType].Invoke(NetworkClient.connection, reader, Channels.Reliable);
        }

        // Branch: netId not present in NetworkClient.spawned — method exits immediately.
        [Test]
        public void OnHostClientObjectHide_UnknownNetId_DoesNothing()
        {
            ConnectHostClientBlockingAuthenticatedAndReady();

            const uint unknownNetId = 9999u;
            Assert.That(NetworkClient.spawned.ContainsKey(unknownNetId), Is.False);

            Assert.DoesNotThrow(() => InvokeObjectHideHandler(unknownNetId));
        }

        // Branch: identity found in spawned but aoi == null — SetHostVisibility is NOT called.
        [Test]
        public void OnHostClientObjectHide_WithoutAoi_DoesNotThrow()
        {
            ConnectHostClientBlockingAuthenticatedAndReady();

            CreateNetworkedAndSpawn(out _, out NetworkIdentity identity);

            Assert.That(NetworkClient.aoi, Is.Null, "aoi must be null for this branch");
            Assert.That(NetworkClient.spawned.ContainsKey(identity.netId), Is.True);

            Assert.DoesNotThrow(() => InvokeObjectHideHandler(identity.netId));
        }

        // Branch: identity found in spawned and aoi != null — SetHostVisibility(identity, false) is called.
        [Test]
        public void OnHostClientObjectHide_WithAoi_CallsSetHostVisibilityFalse()
        {
            ConnectHostClientBlockingAuthenticatedAndReady();

            CreateNetworkedAndSpawn(out _, out NetworkIdentity identity);

            // OnEnable is NOT called in EditMode tests (same reason CreateNetworked
            // calls identity.Awake() manually). Set NetworkClient.aoi directly.
            InterestManagementSpy spy = holder.AddComponent<InterestManagementSpy>();
            NetworkClient.aoi = spy;

            InvokeObjectHideHandler(identity.netId);

            Assert.That(spy.lastIdentity, Is.EqualTo(identity));
            Assert.That(spy.lastVisible,  Is.False);
        }
    }
}