// TODO add true over-the-network movement tests.
//      but we need to split NetworkIdentity.spawned in server/client first.
//      atm we can't spawn an object on both server & client separately yet.
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.NetworkTransformTests
{
    // helper class to expose some of the protected methods
    public class NetworkTransformExposed : NetworkTransformUnreliable
    {
        public new TransformSnapshot Construct() => base.Construct();
        public void Apply(TransformSnapshot interpolated) =>
            base.Apply(interpolated, interpolated);
        public new void OnClientToServerSync(SyncData syncData) =>
            base.OnClientToServerSync(syncData);
        public new void OnServerToClientSync(SyncData syncData) =>
            base.OnServerToClientSync(syncData);
    }

    public class NetworkTransform2kTests : MirrorTest
    {
        // networked and spawned NetworkTransform
        NetworkConnectionToClient connectionToClient;
        Transform                 transform;
        NetworkTransformExposed   component;

        [SetUp]
        public override void SetUp()
        {
            // set up world with server & client
            // host mode for now.
            // TODO separate client & server after .spawned split.
            //      we can use CreateNetworkedAndSpawn that creates on sv & cl.
            //      then move on server, update, verify client position etc.
            base.SetUp();
            NetworkServer.Listen(1);
            ConnectHostClientBlockingAuthenticatedAndReady();
            connectionToClient = NetworkServer.localConnection;

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
            TransformSnapshot from = new TransformSnapshot(
                1,
                1,
                new Vector3(1, 1, 1),
                Quaternion.Euler(new Vector3(0, 0, 0)),
                new Vector3(3, 3, 3)
            );

            TransformSnapshot to = new TransformSnapshot(
                2,
                2,
                new Vector3(2, 2, 2),
                Quaternion.Euler(new Vector3(0, 90, 0)),
                new Vector3(4, 4, 4)
            );

            // interpolate
            TransformSnapshot between = TransformSnapshot.Interpolate(from, to, 0.5);

            // note: timestamp interpolation isn't needed. we don't use it.
            //Assert.That(between.remoteTimestamp, Is.EqualTo(1.5).Within(Mathf.Epsilon));
            //Assert.That(between.localTimestamp, Is.EqualTo(1.5).Within(Mathf.Epsilon));

            // check position
            Assert.That(between.position.x, Is.EqualTo(1.5).Within(Mathf.Epsilon));
            Assert.That(between.position.y, Is.EqualTo(1.5).Within(Mathf.Epsilon));
            Assert.That(between.position.z, Is.EqualTo(1.5).Within(Mathf.Epsilon));

            // check rotation
            // (epsilon is slightly too small)
            Assert.That(between.rotation.eulerAngles.x, Is.EqualTo(0).Within(Mathf.Epsilon));
            Assert.That(between.rotation.eulerAngles.y, Is.EqualTo(45).Within(0.001));
            Assert.That(between.rotation.eulerAngles.z, Is.EqualTo(0).Within(Mathf.Epsilon));

            // check scale
            Assert.That(between.scale.x, Is.EqualTo(3.5).Within(Mathf.Epsilon));
            Assert.That(between.scale.y, Is.EqualTo(3.5).Within(Mathf.Epsilon));
            Assert.That(between.scale.z, Is.EqualTo(3.5).Within(Mathf.Epsilon));
        }

        [Test]
        public void Construct()
        {
            // set unique position/rotation/scale
            transform.position = new Vector3(1, 2, 3);
            transform.rotation = Quaternion.identity;
            transform.localScale = new Vector3(4, 5, 6);

            // construct snapshot
            double time = NetworkTime.localTime;
            TransformSnapshot snapshot = component.Construct();
            Assert.That(snapshot.remoteTime, Is.EqualTo(time).Within(0.01));
            Assert.That(snapshot.position, Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(snapshot.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(snapshot.scale, Is.EqualTo(new Vector3(4, 5, 6)));
        }

        [Test]
        public void Apply()
        {
            // construct snapshot with unique position/rotation/scale
            Vector3 position = new Vector3(1, 2, 3);
            Quaternion rotation = Quaternion.Euler(45, 90, 45);
            Vector3 scale = new Vector3(4, 5, 6);

            // apply snapshot with interpolation
            component.syncPosition = true;
            component.syncRotation = true;
            component.syncScale = true;
            component.Apply(new TransformSnapshot(0, 0, position, rotation, scale));

            // was it applied?
            Assert.That(transform.position, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(transform.rotation, rotation), Is.EqualTo(0).Within(Mathf.Epsilon));
            Assert.That(transform.localScale, Is.EqualTo(scale));
        }

        [Test]
        public void ApplySnapshot_DontSyncPosition()
        {
            // construct snapshot with unique position/rotation/scale
            Vector3 position = new Vector3(1, 2, 3);
            Quaternion rotation = Quaternion.Euler(45, 90, 45);
            Vector3 scale = new Vector3(4, 5, 6);

            // apply snapshot without position sync should not apply position
            component.syncPosition = false;
            component.syncRotation = true;
            component.syncScale = true;
            component.Apply(new TransformSnapshot(0, 0, position, rotation, scale));

            // was it applied?
            Assert.That(transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(Quaternion.Angle(transform.rotation, rotation), Is.EqualTo(0).Within(Mathf.Epsilon));
            Assert.That(transform.localScale, Is.EqualTo(scale));
        }

        [Test]
        public void ApplySnapshot_DontSyncRotation()
        {
            // construct snapshot with unique position/rotation/scale
            Vector3 position = new Vector3(1, 2, 3);
            Quaternion rotation = Quaternion.Euler(45, 90, 45);
            Vector3 scale = new Vector3(4, 5, 6);

            // apply snapshot without position sync should not apply position
            component.syncPosition = true;
            component.syncRotation = false;
            component.syncScale = true;
            component.Apply(new TransformSnapshot(0, 0, position, rotation, scale));

            // was it applied?
            Assert.That(transform.position, Is.EqualTo(position));
            Assert.That(transform.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(transform.localScale, Is.EqualTo(scale));
        }

        [Test]
        public void ApplySnapshot_DontSyncScale()
        {
            // construct snapshot with unique position/rotation/scale
            Vector3 position = new Vector3(1, 2, 3);
            Quaternion rotation = Quaternion.Euler(45, 90, 45);
            Vector3 scale = new Vector3(4, 5, 6);

            // apply snapshot without position sync should not apply position
            component.syncPosition = true;
            component.syncRotation = true;
            component.syncScale = false;
            component.Apply(new TransformSnapshot(0, 0, position, rotation, scale));

            // was it applied?
            Assert.That(transform.position, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(transform.rotation, rotation), Is.EqualTo(0).Within(Mathf.Epsilon));
            Assert.That(transform.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void OnClientToServerSync_WithoutClientAuthority()
        {
            // call OnClientToServerSync without authority
            component.syncDirection = SyncDirection.ServerToClient;
            SyncData syncData = new SyncData((Changed)255, Vector3.zero, Quaternion.identity, Vector3.zero);
            component.OnClientToServerSync(syncData);
            Assert.That(component.serverSnapshots.Count, Is.EqualTo(0));
        }

        [Test]
        public void OnClientToServerSync_WithClientAuthority()
        {
            // call OnClientToServerSync with authority
            component.syncDirection = SyncDirection.ClientToServer;
            SyncData syncData = new SyncData((Changed)255, Vector3.zero, Quaternion.identity, Vector3.zero);
            component.OnClientToServerSync(syncData);
            Assert.That(component.serverSnapshots.Count, Is.EqualTo(1));
        }

        [Test]
        public void OnClientToServerSync_WithClientAuthority_BufferSizeLimit()
        {
            component.connectionToClient.snapshotBufferSizeLimit = 1;

            // authority is required
            component.syncDirection = SyncDirection.ClientToServer;
            SyncData syncData = new SyncData((Changed)255, Vector3.zero, Quaternion.identity, Vector3.zero);
            // add first should work
            component.OnClientToServerSync(syncData);
            Assert.That(component.serverSnapshots.Count, Is.EqualTo(1));
            // add second should be too much
            component.OnClientToServerSync(syncData);
            Assert.That(component.serverSnapshots.Count, Is.EqualTo(1));
        }

        [Test, Ignore("Nullables not supported")]
        public void OnClientToServerSync_WithClientAuthority_Nullables_Uses_Last()
        {
            // set some defaults
            transform.position = Vector3.left;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.right;

            // call OnClientToServerSync with authority and nullable types
            // to make sure it uses the last valid position then.
            component.syncDirection = SyncDirection.ClientToServer;
            SyncData syncData = new SyncData((Changed)255, Vector3.zero, Quaternion.identity, Vector3.zero);
            component.OnClientToServerSync(syncData);
            Assert.That(component.serverSnapshots.Count, Is.EqualTo(1));
            TransformSnapshot first = component.serverSnapshots.Values[0];
            Assert.That(first.position, Is.EqualTo(Vector3.left));
            Assert.That(first.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(first.scale, Is.EqualTo(Vector3.right));
        }

        // server->client sync should only work if client doesn't have authority
        [Test]
        public void OnServerToClientSync_WithoutClientAuthority()
        {
            // pretend to be the client object
            component.netIdentity.isServer = false;
            component.netIdentity.isClient = true;
            component.netIdentity.isLocalPlayer = true;

            SyncData syncData = new SyncData((Changed)255, Vector3.zero, Quaternion.identity, Vector3.zero);

            // call OnServerToClientSync without authority
            component.syncDirection = SyncDirection.ServerToClient;
            component.OnServerToClientSync(syncData);
            Assert.That(component.clientSnapshots.Count, Is.EqualTo(1));
        }

        // server->client sync shouldn't work if client has authority
        [Test]
        public void OnServerToClientSync_WithoutClientAuthority_bufferSizeLimit()
        {
            component.connectionToClient.snapshotBufferSizeLimit = 1;

            // pretend to be the client object
            component.netIdentity.isServer = false;
            component.netIdentity.isClient = true;
            component.netIdentity.isLocalPlayer = true;

            // client authority has to be disabled
            component.syncDirection = SyncDirection.ServerToClient;

            SyncData syncData = new SyncData((Changed)255, Vector3.zero, Quaternion.identity, Vector3.zero);

            // add first should work
            component.OnServerToClientSync(syncData);
            Assert.That(component.clientSnapshots.Count, Is.EqualTo(1));

            // add second should be too much
            component.OnServerToClientSync(syncData);
            Assert.That(component.clientSnapshots.Count, Is.EqualTo(1));
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
            component.syncDirection = SyncDirection.ClientToServer;
            SyncData syncData = new SyncData((Changed)255, Vector3.zero, Quaternion.identity, Vector3.zero);
            component.OnServerToClientSync(syncData);
            Assert.That(component.clientSnapshots.Count, Is.EqualTo(0));
        }

        [Test, Ignore("Nullables not supported")]
        public void OnServerToClientSync_WithClientAuthority_Nullables_Uses_Last()
        {
            // set some defaults
            transform.position = Vector3.left;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.right;

            // pretend to be the client object
            component.netIdentity.isServer = false;
            component.netIdentity.isClient = true;
            component.netIdentity.isLocalPlayer = true;

            // client authority has to be disabled
            component.syncDirection = SyncDirection.ServerToClient;

            // call OnClientToServerSync with authority and nullable types
            // to make sure it uses the last valid position then.
            SyncData syncData = new SyncData((Changed)255, Vector3.zero, Quaternion.identity, Vector3.zero);
            component.OnServerToClientSync(syncData);
            Assert.That(component.clientSnapshots.Count, Is.EqualTo(1));
            TransformSnapshot first = component.clientSnapshots.Values[0];
            Assert.That(first.position, Is.EqualTo(Vector3.left));
            Assert.That(first.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(first.scale, Is.EqualTo(Vector3.right));
        }
    }
}
