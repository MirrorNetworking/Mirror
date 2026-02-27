using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.NetworkClients
{
    public class NetworkClientTests_ReadyAndAddPlayer : MirrorEditModeTest
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            NetworkServer.Listen(1);
        }

        // ?? Ready() ??????????????????????????????????????????????????????????

        [Test]
        public void Ready_ReturnsTrueAndSetsReadyFlag()
        {
            ConnectClientBlocking(out _);
            bool result = NetworkClient.Ready();
            Assert.That(result, Is.True);
            Assert.That(NetworkClient.ready, Is.True);
        }

        [Test]
        public void Ready_AlsoSetsConnectionIsReady()
        {
            ConnectClientBlocking(out _);
            NetworkClient.Ready();
            Assert.That(NetworkClient.connection.isReady, Is.True);
        }

        [Test]
        public void Ready_ErrorWhenAlreadyReady()
        {
            ConnectClientBlocking(out _);
            NetworkClient.Ready();
            LogAssert.Expect(LogType.Error, "NetworkClient is already ready. It shouldn't be called twice.");
            bool result = NetworkClient.Ready();
            Assert.That(result, Is.False);
        }

        [Test]
        public void Ready_ErrorWhenConnectionIsNull()
        {
            // Never connected — connection stays null.
            LogAssert.Expect(LogType.Error, "Ready() called with invalid connection object: conn=null");
            bool result = NetworkClient.Ready();
            Assert.That(result, Is.False);
        }

        // ?? AddPlayer() ??????????????????????????????????????????????????????

        [Test]
        public void AddPlayer_ErrorWhenConnectionIsNull()
        {
            LogAssert.Expect(LogType.Error, "AddPlayer requires a valid NetworkClient.connection.");
            bool result = NetworkClient.AddPlayer();
            Assert.That(result, Is.False);
        }

        [Test]
        public void AddPlayer_ErrorWhenNotReady()
        {
            ConnectClientBlocking(out _);
            // deliberately skip Ready()
            LogAssert.Expect(LogType.Error, "AddPlayer requires a ready NetworkClient.");
            bool result = NetworkClient.AddPlayer();
            Assert.That(result, Is.False);
        }

        [Test]
        public void AddPlayer_ErrorWhenConnectionAlreadyHasIdentity()
        {
            ConnectClientBlocking(out _);
            NetworkClient.Ready();
            CreateNetworked(out _, out NetworkIdentity identity);
            NetworkClient.connection.identity = identity;
            LogAssert.Expect(LogType.Error, "NetworkClient.AddPlayer: a PlayerController was already added. Did you call AddPlayer twice?");
            bool result = NetworkClient.AddPlayer();
            Assert.That(result, Is.False);
        }

        [Test]
        public void AddPlayer_ReturnsTrueWhenReady()
        {
            ConnectClientBlocking(out _);
            NetworkClient.Ready();
            bool result = NetworkClient.AddPlayer();
            Assert.That(result, Is.True);
        }
    }
}