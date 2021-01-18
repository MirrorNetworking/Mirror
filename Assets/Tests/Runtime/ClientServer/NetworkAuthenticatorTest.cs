using System;
using NSubstitute;
using NUnit.Framework;

namespace Mirror.Tests.ClientServer
{
    [TestFixture]
    public class NetworkAuthenticatorTest : ClientServerSetup<MockComponent>
    {
        NetworkAuthenticator serverAuthenticator;
        NetworkAuthenticator clientAuthenticator;

        Action<INetworkConnection> serverMockMethod;
        Action<INetworkConnection> clientMockMethod;


        class NetworkAuthenticationImpl : NetworkAuthenticator { };

        public override void ExtraSetup()
        {
            serverAuthenticator = serverGo.AddComponent<NetworkAuthenticationImpl>();
            clientAuthenticator = clientGo.AddComponent<NetworkAuthenticationImpl>();
            server.authenticator = serverAuthenticator;
            client.authenticator = clientAuthenticator;

            serverMockMethod = Substitute.For<Action<INetworkConnection>>();
            serverAuthenticator.OnServerAuthenticated += serverMockMethod;

            clientMockMethod = Substitute.For<Action<INetworkConnection>>();
            clientAuthenticator.OnClientAuthenticated += clientMockMethod;
        }

        [Test]
        public void OnServerAuthenticateTest()
        {
            serverAuthenticator.OnServerAuthenticate(Substitute.For<INetworkConnection>());

            serverMockMethod.Received().Invoke(Arg.Any<INetworkConnection>());
        }

        [Test]
        public void OnServerAuthenticateInternalTest()
        {
            serverAuthenticator.OnServerAuthenticateInternal(Substitute.For<INetworkConnection>());

            serverMockMethod.Received().Invoke(Arg.Any<INetworkConnection>());
        }

        [Test]
        public void OnClientAuthenticateTest()
        {
            clientAuthenticator.OnClientAuthenticate(Substitute.For<INetworkConnection>());

            clientMockMethod.Received().Invoke(Arg.Any<INetworkConnection>());
        }

        [Test]
        public void OnClientAuthenticateInternalTest()
        {
            clientAuthenticator.OnClientAuthenticateInternal(Substitute.For<INetworkConnection>());

            clientMockMethod.Received().Invoke(Arg.Any<INetworkConnection>());
        }

        [Test]
        public void ClientOnValidateTest()
        {
            Assert.That(client.authenticator, Is.EqualTo(clientAuthenticator));
        }

        [Test]
        public void ServerOnValidateTest()
        {
            Assert.That(server.authenticator, Is.EqualTo(serverAuthenticator));
        }

        [Test]
        public void NetworkClientCallsAuthenticator()
        {
            clientMockMethod.Received().Invoke(Arg.Any<INetworkConnection>());
        }

        [Test]
        public void NetworkServerCallsAuthenticator()
        {
            clientMockMethod.Received().Invoke(Arg.Any<INetworkConnection>());
        }
    }
}
