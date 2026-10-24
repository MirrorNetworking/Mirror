using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.NetworkManagers
{
    [TestFixture]
    public class NetworkManagerTest : MirrorEditModeTest
    {
        GameObject gameObject => holder;
        NetworkManager manager;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            manager = gameObject.AddComponent<NetworkManager>();
            manager.transport = transport;
        }

        [Test]
        public void StartServerTest()
        {
            Assert.That(NetworkServer.active, Is.False);

            manager.StartServer();

            Assert.That(manager.mode == NetworkManagerMode.ServerOnly);
            Assert.That(NetworkServer.active, Is.True);
        }

        public class FailingStartTransport : MemoryTransport
        {
            public readonly InvalidOperationException failure = new InvalidOperationException("ServerStart failed");

            public override void ServerStart() => throw failure;
        }

        public class StartupAuthenticator : NetworkAuthenticator
        {
            public int startCalls;
            public int stopCalls;
            public bool throwOnStop;

            public override void OnStartServer() => ++startCalls;

            public override void OnStopServer()
            {
                ++stopCalls;
                if (throwOnStop)
                {
                    throw new InvalidOperationException("Authenticator cleanup failed");
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailedListenRemovesOnlyManagerAuthenticationListener(bool host)
        {
            FailingStartTransport failingTransport = gameObject.AddComponent<FailingStartTransport>();
            manager.transport = failingTransport;
            Transport.active = failingTransport;
            NetworkServer.listen = true;
            StartupAuthenticator authenticator = gameObject.AddComponent<StartupAuthenticator>();
            manager.authenticator = authenticator;
            int externalCalls = 0;
            authenticator.OnServerAuthenticated.AddListener(_ => ++externalCalls);

            TestDelegate start = host ? (TestDelegate)manager.StartHost : manager.StartServer;
            Assert.That(Assert.Throws<InvalidOperationException>(start), Is.SameAs(failingTransport.failure));
            Assert.That(NetworkServer.active, Is.False);

            // StopServer returns early while inactive, so startup must undo its listener.
            manager.StopServer();
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1);
            authenticator.OnServerAuthenticated.Invoke(connection);
            Assert.That(connection.isAuthenticated, Is.False);
            Assert.That(externalCalls, Is.EqualTo(1));
            Assert.That(authenticator.startCalls, Is.EqualTo(1));
            Assert.That(authenticator.stopCalls, Is.EqualTo(1));

            // Clean up the failed transport, then retry using the normal transport.
            NetworkServer.Shutdown();
            manager.transport = transport;
            Transport.active = transport;
            manager.StartServer();
            authenticator.OnServerAuthenticated.Invoke(connection);
            Assert.That(connection.isAuthenticated, Is.True);
            Assert.That(externalCalls, Is.EqualTo(2));
            manager.StopServer();
            connection.isAuthenticated = false;
            authenticator.OnServerAuthenticated.Invoke(connection);
            Assert.That(connection.isAuthenticated, Is.False);
            Assert.That(externalCalls, Is.EqualTo(3));
            Assert.That(authenticator.startCalls, Is.EqualTo(2));
            Assert.That(authenticator.stopCalls, Is.EqualTo(2));
        }

        [Test]
        public void FailedListenPreservesOriginalExceptionWhenAuthenticatorCleanupThrows()
        {
            FailingStartTransport failingTransport = gameObject.AddComponent<FailingStartTransport>();
            manager.transport = failingTransport;
            Transport.active = failingTransport;
            NetworkServer.listen = true;
            StartupAuthenticator authenticator = gameObject.AddComponent<StartupAuthenticator>();
            manager.authenticator = authenticator;
            authenticator.throwOnStop = true;

            LogAssert.Expect(LogType.Warning, "Authenticator startup rollback failed: InvalidOperationException");
            Assert.That(Assert.Throws<InvalidOperationException>(manager.StartServer), Is.SameAs(failingTransport.failure));
            NetworkConnectionToClient connection = new NetworkConnectionToClient(1);
            authenticator.OnServerAuthenticated.Invoke(connection);
            Assert.That(connection.isAuthenticated, Is.False);
            Assert.That(authenticator.stopCalls, Is.EqualTo(1));
        }

        [Test]
        public void StopServerTest()
        {
            manager.StartServer();
            manager.StopServer();

            Assert.That(manager.mode == NetworkManagerMode.Offline);
        }

        [Test]
        public void StartClientTest()
        {
            manager.StartClient();

            Assert.That(manager.mode == NetworkManagerMode.ClientOnly);

            manager.StopClient();
        }

        [Test]
        public void StopClientTest()
        {
            manager.StartServer();
            UpdateTransport();
            manager.StartClient();
            UpdateTransport();
            manager.StopClient();
            UpdateTransport();

            Assert.That(manager.mode == NetworkManagerMode.Offline);
        }

        [Test]
        public void StartHostTest()
        {
            manager.StartHost();

            Assert.That(manager.mode == NetworkManagerMode.Host);
            Assert.That(NetworkServer.active, Is.True);
            Assert.That(NetworkClient.active, Is.True);
        }

        [Test]
        public void StopHostTest()
        {
            manager.StartHost();
            manager.StopHost();

            Assert.That(manager.mode == NetworkManagerMode.Offline);
            Assert.That(NetworkServer.active, Is.False);
            Assert.That(NetworkClient.active, Is.False);
        }

        [Test]
        public void ShutdownTest()
        {
            manager.StartClient();
            NetworkManager.ResetStatics();

            Assert.That(NetworkManager.startPositions.Count, Is.Zero);
            Assert.That(NetworkManager.startPositionIndex, Is.Zero);
            Assert.That(NetworkManager.clientReadyConnection, Is.Null);
            Assert.That(NetworkManager.loadingSceneAsync, Is.Null);
            Assert.That(NetworkManager.singleton, Is.Null);
            Assert.That(NetworkManager.networkSceneName, Is.Empty);
        }

        [Test]
        public void RegisterStartPositionTest()
        {
            Assert.That(NetworkManager.startPositions.Count, Is.Zero);

            NetworkManager.RegisterStartPosition(gameObject.transform);
            Assert.That(NetworkManager.startPositions.Count, Is.EqualTo(1));
            Assert.That(NetworkManager.startPositions, Has.Member(gameObject.transform));

            NetworkManager.UnRegisterStartPosition(gameObject.transform);
        }

        [Test]
        public void UnRegisterStartPositionTest()
        {
            Assert.That(NetworkManager.startPositions.Count, Is.Zero);

            NetworkManager.RegisterStartPosition(gameObject.transform);
            Assert.That(NetworkManager.startPositions.Count, Is.EqualTo(1));
            Assert.That(NetworkManager.startPositions, Has.Member(gameObject.transform));

            NetworkManager.UnRegisterStartPosition(gameObject.transform);
            Assert.That(NetworkManager.startPositions.Count, Is.Zero);
        }

        [Test]
        public void GetStartPositionTest()
        {
            Assert.That(NetworkManager.startPositions.Count, Is.Zero);

            NetworkManager.RegisterStartPosition(gameObject.transform);
            Assert.That(NetworkManager.startPositions.Count, Is.EqualTo(1));
            Assert.That(NetworkManager.startPositions, Has.Member(gameObject.transform));

            Assert.That(manager.GetStartPosition(), Is.SameAs(gameObject.transform));

            NetworkManager.UnRegisterStartPosition(gameObject.transform);
        }

        [Test]
        public void StartClientUriTest()
        {
            UriBuilder uriBuilder = new UriBuilder
            {
                Host = "localhost",
                Scheme = "local"
            };
            manager.StartClient(uriBuilder.Uri);

            Assert.That(manager.mode, Is.EqualTo(NetworkManagerMode.ClientOnly));
            Assert.That(manager.networkAddress, Is.EqualTo(uriBuilder.Uri.Host));
        }

        [Test]
        public void StopHostAfterStartHostFailureDoesNotThrow()
        {
            // This test simulates the scenario where StartHost fails (e.g., SocketException)
            // The mode is set to Host, but localConnection remains null
            // StopHost/StopClient should not throw NullReferenceException
            
            // Manually set mode to Host to simulate the state after StartHost sets mode but before server setup completes
            typeof(NetworkManager).GetProperty("mode").SetValue(manager, NetworkManagerMode.Host);
            
            // At this point, NetworkServer.localConnection is null (simulating failed startup)
            Assert.That(NetworkServer.localConnection, Is.Null);
            Assert.That(manager.mode, Is.EqualTo(NetworkManagerMode.Host));
            
            // Call StopHost - should not throw NullReferenceException
            Assert.DoesNotThrow(() => manager.StopHost());
        }

        [Test]
        public void StopClientAfterStartHostFailureDoesNotThrow()
        {
            // This test simulates calling StopClient directly when in Host mode with null localConnection
            
            // Manually set mode to Host to simulate the state after StartHost sets mode but before server setup completes
            typeof(NetworkManager).GetProperty("mode").SetValue(manager, NetworkManagerMode.Host);
            
            // At this point, NetworkServer.localConnection is null (simulating failed startup)
            Assert.That(NetworkServer.localConnection, Is.Null);
            Assert.That(manager.mode, Is.EqualTo(NetworkManagerMode.Host));
            
            // Call StopClient - should not throw NullReferenceException
            Assert.DoesNotThrow(() => manager.StopClient());
        }
    }
}
