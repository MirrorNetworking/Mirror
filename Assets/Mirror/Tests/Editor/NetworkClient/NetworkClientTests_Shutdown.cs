using System;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.NetworkClients
{
    /// <summary>
    /// Tests for Shutdown() state-reset behaviour not covered by the existing
    /// ShutdownCleanup test (which already checks handlers, spawned, connection,
    /// and active).
    /// </summary>
    public class NetworkClientTests_Shutdown : MirrorEditModeTest
    {
        // ── Bool state fields ─────────────────────────────────────────────────

        [Test]
        public void Shutdown_ResetsExceptionsDisconnectToTrue()
        {
            NetworkClient.exceptionsDisconnect = false;

            NetworkClient.Shutdown();

            Assert.That(NetworkClient.exceptionsDisconnect, Is.True);
        }

        [Test]
        public void Shutdown_ResetsReadyToFalse()
        {
            NetworkServer.Listen(1);
            ConnectClientBlocking(out _);
            NetworkClient.Ready();
            Assert.That(NetworkClient.ready, Is.True); // precondition

            NetworkClient.Shutdown();

            Assert.That(NetworkClient.ready, Is.False);
        }

        [Test]
        public void Shutdown_ResetsIsLoadingSceneToFalse()
        {
            NetworkClient.isLoadingScene = true;

            NetworkClient.Shutdown();

            Assert.That(NetworkClient.isLoadingScene, Is.False);
        }

        // ── Reference fields ──────────────────────────────────────────────────

        [Test]
        public void Shutdown_ResetsLocalPlayerToNull()
        {
            NetworkServer.Listen(1);
            ConnectClientBlocking(out _);
            NetworkClient.Ready();
            CreateNetworked(out _, out NetworkIdentity identity);
            NetworkClient.InternalAddPlayer(identity);
            Assert.That(NetworkClient.localPlayer, Is.Not.Null); // precondition

            NetworkClient.Shutdown();

            Assert.That(NetworkClient.localPlayer, Is.Null);
        }

        // ── Event delegates ───────────────────────────────────────────────────

        [Test]
        public void Shutdown_ClearsAllEventDelegates()
        {
            NetworkClient.OnConnectedEvent        = () => { };
            NetworkClient.OnDisconnectedEvent     = () => { };
            NetworkClient.OnErrorEvent            = (error, reason) => { };
            NetworkClient.OnTransportExceptionEvent = ex => { };

            NetworkClient.Shutdown();

            Assert.That(NetworkClient.OnConnectedEvent,         Is.Null);
            Assert.That(NetworkClient.OnDisconnectedEvent,      Is.Null);
            Assert.That(NetworkClient.OnErrorEvent,             Is.Null);
            Assert.That(NetworkClient.OnTransportExceptionEvent, Is.Null);
        }
    }
}