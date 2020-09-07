using System;
using System.Collections;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using UnityEngine.TestTools;

using static Mirror.Tests.AsyncUtil;

namespace Mirror.Tests
{
    [TestFixture]
    public class NetworkAuthenticatorTest : ClientServerSetup<MockComponent>
    {
        NetworkAuthenticator serverAuthenticator;
        NetworkAuthenticator clientAuthenticator;

        class NetworkAuthenticationImpl : NetworkAuthenticator { };

        public override void ExtraSetup()
        {
            serverAuthenticator = serverGo.AddComponent<NetworkAuthenticationImpl>();
            clientAuthenticator = clientGo.AddComponent<NetworkAuthenticationImpl>();
            server.authenticator = serverAuthenticator;
            client.authenticator = clientAuthenticator;
        }

        [Test]
        public void OnServerAuthenticateTest()
        {
            Action<INetworkConnection> mockMethod = Substitute.For<Action<INetworkConnection>>();
            serverAuthenticator.OnServerAuthenticated += mockMethod;

            serverAuthenticator.OnServerAuthenticate(Substitute.For<INetworkConnection>());

            mockMethod.Received().Invoke(Arg.Any<INetworkConnection>());
        }

        [Test]
        public void OnServerAuthenticateInternalTest()
        {
            Action<INetworkConnection> mockMethod = Substitute.For<Action<INetworkConnection>>();
            serverAuthenticator.OnServerAuthenticated += mockMethod;

            serverAuthenticator.OnServerAuthenticateInternal(Substitute.For<INetworkConnection>());

            mockMethod.Received().Invoke(Arg.Any<INetworkConnection>());
        }

        [Test]
        public void OnClientAuthenticateTest()
        {
            Action<INetworkConnection> mockMethod = Substitute.For<Action<INetworkConnection>>();
            clientAuthenticator.OnClientAuthenticated += mockMethod;

            clientAuthenticator.OnClientAuthenticate(Substitute.For<INetworkConnection>());

            mockMethod.Received().Invoke(Arg.Any<INetworkConnection>());
        }

        [Test]
        public void OnClientAuthenticateInternalTest()
        {
            Action<INetworkConnection> mockMethod = Substitute.For<Action<INetworkConnection>>();
            clientAuthenticator.OnClientAuthenticated += mockMethod;

            clientAuthenticator.OnClientAuthenticateInternal(Substitute.For<INetworkConnection>());

            mockMethod.Received().Invoke(Arg.Any<INetworkConnection>());
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

        [UnityTest]
        public IEnumerator NetworkClientCallsAuthenticator() => RunAsync(async () =>
        {
            Action<INetworkConnection> mockMethod = Substitute.For<Action<INetworkConnection>>();
            clientAuthenticator.OnClientAuthenticated += mockMethod;

            await Task.Delay(1);

            client.ConnectHost(server);

            mockMethod.Received().Invoke(Arg.Any<INetworkConnection>());

            client.Disconnect();
        });

        [UnityTest]
        public IEnumerator NetworkServerCallsAuthenticator() => RunAsync(async () =>
        {
            Action<INetworkConnection> mockMethod = Substitute.For<Action<INetworkConnection>>();
            serverAuthenticator.OnServerAuthenticated += mockMethod;

            await Task.Delay(1);

            client.ConnectHost(server);

            await Task.Delay(1);

            mockMethod.Received().Invoke(Arg.Any<INetworkConnection>());

            client.Disconnect();
        });
    }
}
