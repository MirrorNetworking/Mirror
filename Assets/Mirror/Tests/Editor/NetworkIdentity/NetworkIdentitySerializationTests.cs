// OnDe/SerializeSafely tests.
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.NetworkIdentities
{
    public class NetworkIdentitySerializationTests : MirrorEditModeTest
    {
        // writers are always needed. create in setup for convenience.
        NetworkWriter ownerWriter;
        NetworkWriter observersWriter;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            ownerWriter = new NetworkWriter();
            observersWriter = new NetworkWriter();

            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out _);
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        class ParentNesting
        {
            public class SyncVarTest1NetworkBehaviour : NetworkBehaviour
            {
                [SyncVar] public int value;
            }
        }

        // server should still broadcast ClientToServer components to everyone
        // except the owner.
        [Test]
        public void SerializeServer_ObserversMode_ClientToServer()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity,
                out ParentNesting.SyncVarTest1NetworkBehaviour comp);

            // pretend to be owned
            identity.isOwned = true;
            comp.syncMode = SyncMode.Observers;
            comp.syncInterval = 0;

            // set to CLIENT with some unique values
            // and set connection to server to pretend we are the owner.
            comp.syncDirection = SyncDirection.ClientToServer;
            comp.value = 12345;

            // initial: should write something for owner and observers
            identity.SerializeServer(true, ownerWriter, observersWriter);
            Debug.Log("initial ownerWriter: " + ownerWriter);
            Debug.Log("initial observerWriter: " + observersWriter);
            Assert.That(ownerWriter.Position, Is.GreaterThan(0));
            Assert.That(observersWriter.Position, Is.GreaterThan(0));

            // delta: should only write for observers
            ++comp.value; // change something
            ownerWriter.Position = 0;
            observersWriter.Position = 0;
            identity.SerializeServer(false, ownerWriter, observersWriter);
            Debug.Log("delta ownerWriter: " + ownerWriter);
            Debug.Log("delta observersWriter: " + observersWriter);
            Assert.That(ownerWriter.Position, Is.EqualTo(0));
            Assert.That(observersWriter.Position, Is.GreaterThan(0));
        }
    }
}
