// TODO add true over-the-network movement tests.
//      but we need to split NetworkIdentity.spawned in server/client first.
//      atm we can't spawn an object on both server & client separately yet.
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.NetworkTransform2k
{
    // helper class to expose some of the protected methods
    public class NetworkTransformExposed : NetworkTransform
    {
        public new NTSnapshot ConstructSnapshot() => base.ConstructSnapshot();
        public new void ApplySnapshot(NTSnapshot start, NTSnapshot goal, NTSnapshot interpolated) =>
            base.ApplySnapshot(start, goal, interpolated);
        public new void OnClientToServerSync(Vector3? position, Quaternion? rotation, Vector3? scale) =>
            base.OnClientToServerSync(position, rotation, scale);
        public new void OnServerToClientSync(Vector3? position, Quaternion? rotation, Vector3? scale) =>
            base.OnServerToClientSync(position, rotation, scale);
    }

    public class NetworkTransform2kTests : MirrorTest
    {
        // networked and spawned NetworkTransform
        NetworkConnectionToClient connectionToClient;
        Transform transform;
        NetworkTransformExposed component;

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
            NTSnapshot between = NTSnapshot.Interpolate(from, to, 0.5);

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
            Assert.That(snapshot.position, Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(snapshot.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(snapshot.scale, Is.EqualTo(new Vector3(4, 5, 6)));
        }

        [Test]
        public void ApplySnapshot_Interpolated()
        {
            // construct snapshot with unique position/rotation/scale
            Vector3 position = new Vector3(1, 2, 3);
            Quaternion rotation = Quaternion.Euler(45, 90, 45);
            Vector3 scale = new Vector3(4, 5, 6);

            // apply snapshot with interpolation
            component.syncPosition = true;
            component.syncRotation = true;
            component.syncScale = true;
            component.interpolatePosition = true;
            component.interpolateRotation = true;
            component.interpolateScale = true;
            component.ApplySnapshot(default, default, new NTSnapshot(0, 0, position, rotation, scale));

            // was it applied?
            Assert.That(transform.position, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(transform.rotation, rotation), Is.EqualTo(0).Within(Mathf.Epsilon));
            Assert.That(transform.localScale, Is.EqualTo(scale));
        }

        [Test]
        public void ApplySnapshot_Direct()
        {
            // construct snapshot with unique position/rotation/scale
            Vector3 position = new Vector3(1, 2, 3);
            Quaternion rotation = Quaternion.Euler(45, 90, 45);
            Vector3 scale = new Vector3(4, 5, 6);

            // apply snapshot without interpolation
            component.syncPosition = true;
            component.syncRotation = true;
            component.syncScale = true;
            component.interpolatePosition = false;
            component.interpolateRotation = false;
            component.interpolateScale = false;
            component.ApplySnapshot(default, new NTSnapshot(0, 0, position, rotation, scale), default);

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
            component.interpolatePosition = false;
            component.interpolateRotation = true;
            component.interpolateScale = true;
            component.ApplySnapshot(default, default, new NTSnapshot(0, 0, position, rotation, scale));

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
            component.interpolatePosition = true;
            component.interpolateRotation = false;
            component.interpolateScale = true;
            component.ApplySnapshot(default, default, new NTSnapshot(0, 0, position, rotation, scale));

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
            component.interpolatePosition = true;
            component.interpolateRotation = true;
            component.interpolateScale = false;
            component.ApplySnapshot(default, default, new NTSnapshot(0, 0, position, rotation, scale));

            // was it applied?
            Assert.That(transform.position, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(transform.rotation, rotation), Is.EqualTo(0).Within(Mathf.Epsilon));
            Assert.That(transform.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void OnClientToServerSync_WithoutClientAuthority()
        {
            // call OnClientToServerSync without authority
            component.clientAuthority = false;
            component.OnClientToServerSync(Vector3.zero, Quaternion.identity, Vector3.zero);
            Assert.That(component.serverBuffer.Count, Is.EqualTo(0));
        }

        [Test]
        public void OnClientToServerSync_WithClientAuthority()
        {
            // call OnClientToServerSync with authority
            component.clientAuthority = true;
            component.OnClientToServerSync(Vector3.zero, Quaternion.identity, Vector3.zero);
            Assert.That(component.serverBuffer.Count, Is.EqualTo(1));
        }

        [Test]
        public void OnClientToServerSync_WithClientAuthority_BufferSizeLimit()
        {
            component.bufferSizeLimit = 1;

            // authority is required
            component.clientAuthority = true;

            // add first should work
            component.OnClientToServerSync(Vector3.zero, Quaternion.identity, Vector3.zero);
            Assert.That(component.serverBuffer.Count, Is.EqualTo(1));

            // add second should be too much
            component.OnClientToServerSync(Vector3.zero, Quaternion.identity, Vector3.zero);
            Assert.That(component.serverBuffer.Count, Is.EqualTo(1));
        }

        [Test]
        public void OnClientToServerSync_WithClientAuthority_Nullables_Uses_Last()
        {
            // set some defaults
            transform.position = Vector3.left;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.right;

            // call OnClientToServerSync with authority and nullable types
            // to make sure it uses the last valid position then.
            component.clientAuthority = true;
            component.OnClientToServerSync(new Vector3?(), new Quaternion?(), new Vector3?());
            Assert.That(component.serverBuffer.Count, Is.EqualTo(1));
            NTSnapshot first = component.serverBuffer.Values[0];
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

            // call OnServerToClientSync without authority
            component.clientAuthority = false;
            component.OnServerToClientSync(Vector3.zero, Quaternion.identity, Vector3.zero);
            Assert.That(component.clientBuffer.Count, Is.EqualTo(1));
        }

        // server->client sync shouldn't work if client has authority
        [Test]
        public void OnServerToClientSync_WithoutClientAuthority_bufferSizeLimit()
        {
            component.bufferSizeLimit = 1;

            // pretend to be the client object
            component.netIdentity.isServer = false;
            component.netIdentity.isClient = true;
            component.netIdentity.isLocalPlayer = true;

            // client authority has to be disabled
            component.clientAuthority = false;

            // add first should work
            component.OnServerToClientSync(Vector3.zero, Quaternion.identity, Vector3.zero);
            Assert.That(component.clientBuffer.Count, Is.EqualTo(1));

            // add second should be too much
            component.OnServerToClientSync(Vector3.zero, Quaternion.identity, Vector3.zero);
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
            component.OnServerToClientSync(Vector3.zero, Quaternion.identity, Vector3.zero);
            Assert.That(component.clientBuffer.Count, Is.EqualTo(0));
        }

        [Test]
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

            // call OnClientToServerSync with authority and nullable types
            // to make sure it uses the last valid position then.
            component.OnServerToClientSync(new Vector3?(), new Quaternion?(), new Vector3?());
            Assert.That(component.clientBuffer.Count, Is.EqualTo(1));
            NTSnapshot first = component.clientBuffer.Values[0];
            Assert.That(first.position, Is.EqualTo(Vector3.left));
            Assert.That(first.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(first.scale, Is.EqualTo(Vector3.right));
        }
    }
}
