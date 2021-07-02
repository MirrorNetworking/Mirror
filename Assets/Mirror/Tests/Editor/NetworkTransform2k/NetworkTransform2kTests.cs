// TODO add true over-the-network movement tests.
//      but we need to split NetworkIdentity.spawned in server/client first.
//      atm we can't spawn an object on both server & client separately yet.
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.NetworkTransform2k
{
    public class NetworkTransform2kTests : MirrorTest
    {
        // networked and spawned NetworkTransform
        NetworkConnectionToClient connectionToClient;
        Transform transform;
        NetworkTransform component;

        [SetUp]
        public override void SetUp()
        {
            // set up world with server & client
            base.SetUp();
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out connectionToClient);

            // create a networked object with NetworkTransform
            CreateNetworkedAndSpawn(out GameObject go, out NetworkIdentity _, out component, connectionToClient);
            // sync immediately
            component.syncInterval = 0;
            // remember transform for convenience
            transform = go.transform;
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
            NetworkClient.Disconnect();
        }

        // TODO move to NTSnapshot tests?
        [Test]
        public void Interpolate()
        {
            NTSnapshot from = new NTSnapshot(
                1,
                1,
                new Vector3(1, 1, 1),
                Quaternion.Euler(new Vector3(0, 0, 0)),
                new Vector3(3, 3, 3)
            );

            NTSnapshot to = new NTSnapshot(
                2,
                2,
                new Vector3(2, 2, 2),
                Quaternion.Euler(new Vector3(0, 90, 0)),
                new Vector3(4, 4, 4)
            );

            // interpolate
            NTSnapshot between = (NTSnapshot)from.Interpolate(to, 0.5);

            // check time
            Assert.That(between.remoteTimestamp, Is.EqualTo(1.5).Within(Mathf.Epsilon));
            Assert.That(between.localTimestamp, Is.EqualTo(1.5).Within(Mathf.Epsilon));

            // check position
            Assert.That(between.transform.position.x, Is.EqualTo(1.5).Within(Mathf.Epsilon));
            Assert.That(between.transform.position.y, Is.EqualTo(1.5).Within(Mathf.Epsilon));
            Assert.That(between.transform.position.z, Is.EqualTo(1.5).Within(Mathf.Epsilon));

            // check rotation
            // (epsilon is slightly too small)
            Assert.That(between.transform.rotation.eulerAngles.x, Is.EqualTo(0).Within(Mathf.Epsilon));
            Assert.That(between.transform.rotation.eulerAngles.y, Is.EqualTo(45).Within(0.001));
            Assert.That(between.transform.rotation.eulerAngles.z, Is.EqualTo(0).Within(Mathf.Epsilon));

            // check scale
            Assert.That(between.transform.scale.x, Is.EqualTo(3.5).Within(Mathf.Epsilon));
            Assert.That(between.transform.scale.y, Is.EqualTo(3.5).Within(Mathf.Epsilon));
            Assert.That(between.transform.scale.z, Is.EqualTo(3.5).Within(Mathf.Epsilon));
        }

        [Test]
        public void ConstructSnapshot()
        {
            // set unique position/rotation/scale
            transform.position = new Vector3(1, 2, 3);
            transform.rotation = Quaternion.identity;
            transform.localScale = new Vector3(4, 5, 6);

            // construct snapshot
            double time = NetworkTime.localTime;
            NTSnapshot snapshot = component.ConstructSnapshot();
            Assert.That(snapshot.remoteTimestamp, Is.EqualTo(time).Within(0.01));
            Assert.That(snapshot.transform.position, Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(snapshot.transform.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(snapshot.transform.scale, Is.EqualTo(new Vector3(4, 5, 6)));
        }

        [Test]
        public void ApplySnapshot()
        {
            // construct snapshot with unique position/rotation/scale
            Vector3 position = new Vector3(1, 2, 3);
            Quaternion rotation = Quaternion.identity;
            Vector3 scale = new Vector3(4, 5, 6);

            // apply snapshot
            component.ApplySnapshot(new NTSnapshot(0, 0, position, rotation, scale));

            // was it applied?
            Assert.That(transform.position, Is.EqualTo(position));
            Assert.That(transform.rotation, Is.EqualTo(rotation));
            Assert.That(transform.localScale, Is.EqualTo(scale));
        }

        [Test]
        public void OnClientToServerSync_WithoutClientAuthority()
        {
            // call OnClientToServerSync without authority
            component.clientAuthority = false;
            component.OnClientToServerSync(new NTSnapshotTransform());
            Assert.That(component.serverBuffer.Count, Is.EqualTo(0));
        }

        [Test]
        public void OnClientToServerSync_WithClientAuthority()
        {
            // call OnClientToServerSync with authority
            component.clientAuthority = true;
            component.OnClientToServerSync(new NTSnapshotTransform());
            Assert.That(component.serverBuffer.Count, Is.EqualTo(1));
        }

        // server->client sync should only work if client doesn't have authority
        [Test]
        public void OnServerToClientSync_WithoutClientAuthority()
        {
            // pretend to be the client object
            component.netIdentity.isServer = false;
            component.netIdentity.isClient = true;
            component.netIdentity.isLocalPlayer = true;

            // call OnServerToClientSync without authority
            component.clientAuthority = false;
            component.OnServerToClientSync(new NTSnapshotTransform());
            Assert.That(component.clientBuffer.Count, Is.EqualTo(1));
        }

        // server->client sync shouldn't work if client has authority
        [Test]
        public void OnServerToClientSync_WithClientAuthority()
        {
            // pretend to be the client object
            component.netIdentity.isServer = false;
            component.netIdentity.isClient = true;
            component.netIdentity.isLocalPlayer = true;

            // call OnServerToClientSync with authority
            component.clientAuthority = true;
            component.OnServerToClientSync(new NTSnapshotTransform());
            Assert.That(component.clientBuffer.Count, Is.EqualTo(0));
        }
    }
}
