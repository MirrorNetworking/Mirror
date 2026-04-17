// Make sure this class properly extends the test base and has explicit SetUp/TearDown
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.NetworkIdentities
{
    public class NetworkIdentityStateTests : MirrorEditModeTest
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        [Test]
        public void IsClient_NeverResetAfterSet()
        {
            // Create identity without spawning - we only care about the flag
            CreateNetworked(out GameObject go, out NetworkIdentity identity);
            
            // Manually set isClient (simulating what happens during spawning)
            identity.isClient = true;
            
            Assert.That(identity.isClient, Is.True);
            
            // Stop client - isClient should STILL be true (issue #1475)
            // This is the documented behavior: once set, never reset
            NetworkClient.Shutdown();
            Assert.That(identity.isClient, Is.True, 
                "isClient should never be reset after being set (issue #1475)");
        }
        
        [Test]
        public void IsServer_NeverResetAfterSet()
        {
            // Create identity without spawning
            CreateNetworked(out GameObject go, out NetworkIdentity identity);
            
            // Manually set isServer (simulating what happens during spawning)
            identity.isServer = true;
            
            Assert.That(identity.isServer, Is.True);
            
            // Stop server - isServer should STILL be true (issue #1484, #2533)
            // This is the documented behavior: once set, never reset
            NetworkServer.Shutdown();
            Assert.That(identity.isServer, Is.True,
                "isServer should never be reset after being set (issue #1484, #2533)");
        }
        
        [Test]
        public void IsLocalPlayer_NeverResetAfterSet()
        {
            // Create identity without spawning
            CreateNetworked(out GameObject _, out NetworkIdentity identity);
            
            // Manually set isLocalPlayer (simulating what happens when assigned)
            identity.isLocalPlayer = true;
            
            Assert.That(identity.isLocalPlayer, Is.True);
            
            // isLocalPlayer should persist until ResetState is explicitly called
            // (issue #2615 - components need to read this in OnDestroy)
            Assert.That(identity.isLocalPlayer, Is.True,
                "isLocalPlayer should stay true until ResetState (issue #2615)");
            
            // ResetState is the only thing that clears it
            identity.ResetState();
            Assert.That(identity.isLocalPlayer, Is.False,
                "ResetState should clear isLocalPlayer");
        }
        
        [Test]
        public void IsHost_CombinesIsServerAndIsClient()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity);
            
            // isHost should be false when neither flag is set
            identity.isServer = false;
            identity.isClient = false;
            Assert.That(identity.isHost, Is.False);
            
            // isHost should be false when only server
            identity.isServer = true;
            identity.isClient = false;
            Assert.That(identity.isHost, Is.False);
            
            // isHost should be false when only client
            identity.isServer = false;
            identity.isClient = true;
            Assert.That(identity.isHost, Is.False);
            
            // isHost should be true when both flags are set
            identity.isServer = true;
            identity.isClient = true;
            Assert.That(identity.isHost, Is.True);
        }
        
        [Test]
        public void IsServerOnly_ExcludesClient()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity);
            
            // isServerOnly should be false when neither flag is set
            identity.isServer = false;
            identity.isClient = false;
            Assert.That(identity.isServerOnly, Is.False);
            
            // isServerOnly should be true when only server
            identity.isServer = true;
            identity.isClient = false;
            Assert.That(identity.isServerOnly, Is.True);
            
            // isServerOnly should be false when only client
            identity.isServer = false;
            identity.isClient = true;
            Assert.That(identity.isServerOnly, Is.False);
            
            // isServerOnly should be false when both (host mode)
            identity.isServer = true;
            identity.isClient = true;
            Assert.That(identity.isServerOnly, Is.False);
        }
        
        [Test]
        public void IsClientOnly_ExcludesServer()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity);
            
            // isClientOnly should be false when neither flag is set
            identity.isServer = false;
            identity.isClient = false;
            Assert.That(identity.isClientOnly, Is.False);
            
            // isClientOnly should be false when only server
            identity.isServer = true;
            identity.isClient = false;
            Assert.That(identity.isClientOnly, Is.False);
            
            // isClientOnly should be true when only client
            identity.isServer = false;
            identity.isClient = true;
            Assert.That(identity.isClientOnly, Is.True);
            
            // isClientOnly should be false when both (host mode)
            identity.isServer = true;
            identity.isClient = true;
            Assert.That(identity.isClientOnly, Is.False);
        }
        
        [Test]
        public void IsOwned_TriggersAuthorityCallbacks()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity, out NetworkBehaviourMock comp);
            
            // Initially not owned
            Assert.That(identity.isOwned, Is.False);
            Assert.That(comp.onStartAuthorityCalled, Is.EqualTo(0));
            Assert.That(comp.onStopAuthorityCalled, Is.EqualTo(0));
            
            // Set owned to true - should trigger OnStartAuthority
            identity.isOwned = true;
            identity.NotifyAuthority();
            Assert.That(comp.onStartAuthorityCalled, Is.EqualTo(1));
            Assert.That(comp.onStopAuthorityCalled, Is.EqualTo(0));
            
            // Set owned to false - should trigger OnStopAuthority
            identity.isOwned = false;
            identity.NotifyAuthority();
            Assert.That(comp.onStartAuthorityCalled, Is.EqualTo(1));
            Assert.That(comp.onStopAuthorityCalled, Is.EqualTo(1));
        }
        
        [Test]
        public void ResetState_ClearsAllFlags()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity identity);
            
            // Set all flags
            identity.isServer = true;
            identity.isClient = true;
            identity.isLocalPlayer = true;
            identity.isOwned = true;
            identity.netId = 42;
            
            // Reset state
            identity.ResetState();
            
            // Verify all flags cleared
            Assert.That(identity.isServer, Is.False, "isServer should be cleared");
            Assert.That(identity.isClient, Is.False, "isClient should be cleared");
            Assert.That(identity.isLocalPlayer, Is.False, "isLocalPlayer should be cleared");
            Assert.That(identity.isOwned, Is.False, "isOwned should be cleared");
            Assert.That(identity.netId, Is.EqualTo(0), "netId should be cleared");
        }
    }
}