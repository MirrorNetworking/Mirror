using System;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mirror.Tests
{
    [TestFixture]
    public class NetworkAuthenticatorTest : ClientServerSetup<MockComponent>
    {
        GameObject gameObject;
        NetworkAuthenticator testAuthenticator;

        class NetworkAuthenticationImpl : NetworkAuthenticator { };

        [SetUp]
        public void SetupTest()
        {
            gameObject = new GameObject();

            client = gameObject.AddComponent<NetworkClient>();
            server = gameObject.AddComponent<NetworkServer>();
            testAuthenticator = gameObject.AddComponent<NetworkAuthenticationImpl>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(gameObject);
        }
        
        [Test]
        public void OnServerAuthenticateTest()
        {
            Action<INetworkConnection> mockMethod = Substitute.For<Action<INetworkConnection>>();
            testAuthenticator.OnServerAuthenticated += mockMethod;

            testAuthenticator.OnServerAuthenticate(Substitute.For<INetworkConnection>());

            mockMethod.Received().Invoke(Arg.Any<INetworkConnection>());
        }

        [Test]
        public void OnServerAuthenticateInternalTest()
        {
            Action<INetworkConnection> mockMethod = Substitute.For<Action<INetworkConnection>>();
            testAuthenticator.OnServerAuthenticated += mockMethod;

            testAuthenticator.OnServerAuthenticateInternal(Substitute.For<INetworkConnection>());

            mockMethod.Received().Invoke(Arg.Any<INetworkConnection>());
        }

        [Test]
        public void OnClientAuthenticateTest()
        {
            Action<INetworkConnection> mockMethod = Substitute.For<Action<INetworkConnection>>();
            testAuthenticator.OnClientAuthenticated += mockMethod;

            testAuthenticator.OnClientAuthenticate(Substitute.For<INetworkConnection>());

            mockMethod.Received().Invoke(Arg.Any<INetworkConnection>());
        }

        [Test]
        public void OnClientAuthenticateInternalTest()
        {
            Action<INetworkConnection> mockMethod = Substitute.For<Action<INetworkConnection>>();
            testAuthenticator.OnClientAuthenticated += mockMethod;

            testAuthenticator.OnClientAuthenticateInternal(Substitute.For<INetworkConnection>());

            mockMethod.Received().Invoke(Arg.Any<INetworkConnection>());
        }

        [Test]
        public void ClientOnValidateTest()
        {
            Assert.That(client.authenticator, Is.EqualTo(testAuthenticator));
        }

        [Test]
        public void ServerOnValidateTest()
        {
            Assert.That(server.authenticator, Is.EqualTo(testAuthenticator));
        }
    }
}
