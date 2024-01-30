// default = no component = everyone sees everyone

using System;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.InterestManagement
{
    public class InterestManagementTests_FastSpatialHashing : MirrorEditModeTest
    {
        FastSpatialInterestManagement aoi;

        protected NetworkIdentity CreateNI(Action<NetworkIdentity> prespawn = null)
        {
            CreateNetworked(out var gameObject, out var identity);
            prespawn?.Invoke(identity);
            NetworkServer.Spawn(gameObject);
            return identity;
        }

        protected NetworkIdentity CreatePlayerNI(int connectionId, Action<NetworkIdentity> prespawn = null)
        {
            CreateNetworked(out var gameObject, out var identity);
            prespawn?.Invoke(identity);
            NetworkConnectionToClient connection = new NetworkConnectionToClient(connectionId);
            connection.isAuthenticated = true;
            connection.identity = identity;
            if (!NetworkServer.connections.TryAdd(connectionId, connection))
            {
                throw new Exception("Duplicate connection id");
            }

            NetworkServer.Spawn(gameObject, connection);
            NetworkServer.SetClientReady(connection); // AddPlayerForConnection also calls this!
            return identity;
        }

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            // need to start server so that interest management works
            NetworkServer.Listen(10);

            aoi = holder.AddComponent<FastSpatialInterestManagement>();
            aoi.visRange = 10;
            // setup server aoi since InterestManagement Awake isn't called
            NetworkServer.aoi = aoi;
        }

        [TearDown]
        public override void TearDown()
        {
            foreach (GameObject go in instantiated)
            {
                if (go.TryGetComponent(out NetworkIdentity ni))
                {
                    // set isServer is false. otherwise Destroy instead of
                    // DestroyImmediate is called internally, giving an error in Editor
                    ni.isServer = false;
                }
            }

            // clear connections first. calling OnDisconnect wouldn't work since
            // we have no real clients.
            NetworkServer.connections.Clear();

            base.TearDown();
            // clear server aoi again
            NetworkServer.aoi = null;
        }

        private void AssertSelfVisible(NetworkIdentity id)
        {
            // identities ALWAYS see themselves, if they have a player
            if (id.connectionToClient != null)
            {
                Assert.That(id.observers.ContainsKey(id.connectionToClient.connectionId), Is.True);
            }
        }

        [Test]
        public void ForceHidden()
        {
            // A and B are at (0,0,0) so within range!
            var a = CreatePlayerNI(1, ni => ni.visibility = Visibility.ForceHidden);
            var b = CreatePlayerNI(2);

            // no rebuild required here due to initial state :)

            AssertSelfVisible(a);
            AssertSelfVisible(b);
            // A should not be seen by B because A is force hidden
            Assert.That(a.observers.ContainsKey(b.connectionToClient.connectionId), Is.False);
            // B should be seen by A
            Assert.That(b.observers.ContainsKey(a.connectionToClient.connectionId), Is.True);

            // If we now set a to default, and rebuild, they should both see each other!
            a.visibility = Visibility.Default;
            aoi.LateUpdate();
            AssertSelfVisible(a);
            AssertSelfVisible(b);
            Assert.That(a.observers.ContainsKey(b.connectionToClient.connectionId), Is.True);
            Assert.That(b.observers.ContainsKey(a.connectionToClient.connectionId), Is.True);
            // If we now set both hidden, and rebuild, they both won't see each other!
            a.visibility = Visibility.ForceHidden;
            b.visibility = Visibility.ForceHidden;
            aoi.LateUpdate();
            AssertSelfVisible(a);
            AssertSelfVisible(b);
            Assert.That(a.observers.ContainsKey(b.connectionToClient.connectionId), Is.False);
            Assert.That(b.observers.ContainsKey(a.connectionToClient.connectionId), Is.False);
        }

        [Test]
        public void ForceShown()
        {
            // A and B are at (0,0,0) so within range!
            var a = CreatePlayerNI(1, ni => ni.visibility = Visibility.ForceShown);
            var b = CreatePlayerNI(2);

            // no rebuild required here due to initial state :)
            AssertSelfVisible(a);
            AssertSelfVisible(b);
            // A&B should see each other
            Assert.That(a.observers.ContainsKey(b.connectionToClient.connectionId), Is.True);
            Assert.That(b.observers.ContainsKey(a.connectionToClient.connectionId), Is.True);
            aoi.LateUpdate();
            // rebuild doesnt change that
            // no rebuild required here due to initial state :)
            AssertSelfVisible(a);
            AssertSelfVisible(b);
            // A&B should see each other
            Assert.That(a.observers.ContainsKey(b.connectionToClient.connectionId), Is.True);
            Assert.That(b.observers.ContainsKey(a.connectionToClient.connectionId), Is.True);

            // If we now move A out of range of B
            a.transform.position = new Vector3(aoi.visRange * 100, 0, 0);
            aoi.LateUpdate();
            AssertSelfVisible(a);
            AssertSelfVisible(b);

            // a will be seen by B still
            Assert.That(a.observers.ContainsKey(b.connectionToClient.connectionId), Is.True);
            // But B is out of range of A
            Assert.That(b.observers.ContainsKey(a.connectionToClient.connectionId), Is.False);


            // B to ForceShown:
            b.visibility = Visibility.ForceShown;
            aoi.LateUpdate();
            AssertSelfVisible(a);
            AssertSelfVisible(b);
            // A&B should see each other
            Assert.That(a.observers.ContainsKey(b.connectionToClient.connectionId), Is.True);
            Assert.That(b.observers.ContainsKey(a.connectionToClient.connectionId), Is.True);
        }

        [Test]
        public void InRangeInitial_To_OutRange()
        {
            // A and B are at (0,0,0) so within range!
            var a = CreatePlayerNI(1);
            var b = CreatePlayerNI(2);
            AssertSelfVisible(a);
            AssertSelfVisible(b);
            // both should see each other because they are in range
            Assert.That(a.observers.ContainsKey(b.connectionToClient.connectionId), Is.True);
            Assert.That(b.observers.ContainsKey(a.connectionToClient.connectionId), Is.True);
            // update won't change that
            aoi.LateUpdate();
            AssertSelfVisible(a);
            AssertSelfVisible(b);
            Assert.That(a.observers.ContainsKey(b.connectionToClient.connectionId), Is.True);
            Assert.That(b.observers.ContainsKey(a.connectionToClient.connectionId), Is.True);
            // move out of range
            a.transform.position = new Vector3(aoi.visRange * 100, 0, 0);
            aoi.LateUpdate();
            AssertSelfVisible(a);
            AssertSelfVisible(b);
            // and they'll see not each other anymore
            Assert.That(a.observers.ContainsKey(b.connectionToClient.connectionId), Is.False);
            Assert.That(b.observers.ContainsKey(a.connectionToClient.connectionId), Is.False);
        }

        [Test]
        public void OutRangeInitial_To_InRange()
        {
            // A and B are not in range
            var a = CreatePlayerNI(1,
                ni => ni.transform.position = new Vector3(aoi.visRange * 100, 0, 0));
            var b = CreatePlayerNI(2);

            AssertSelfVisible(a);
            AssertSelfVisible(b);
            // both should not see each other because they aren't in range
            Assert.That(a.observers.ContainsKey(b.connectionToClient.connectionId), Is.False);
            Assert.That(b.observers.ContainsKey(a.connectionToClient.connectionId), Is.False);
            aoi.LateUpdate();
            AssertSelfVisible(a);
            AssertSelfVisible(b);
            // update won't change that
            Assert.That(a.observers.ContainsKey(b.connectionToClient.connectionId), Is.False);
            Assert.That(b.observers.ContainsKey(a.connectionToClient.connectionId), Is.False);
            // move into range
            a.transform.position = Vector3.zero;
            aoi.LateUpdate();
            AssertSelfVisible(a);
            AssertSelfVisible(b);
            // and they'll see each other
            Assert.That(a.observers.ContainsKey(b.connectionToClient.connectionId), Is.True);
            Assert.That(b.observers.ContainsKey(a.connectionToClient.connectionId), Is.True);
        }
    }
}
