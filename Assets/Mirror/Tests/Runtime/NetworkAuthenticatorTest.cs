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
        NetworkAuthenticator testAuthenticator;

        class NetworkAuthenticationImpl : NetworkAuthenticator { };

        public override void ExtraSetup()
        {
            testAuthenticator = networkManagerGo.AddComponent<NetworkAuthenticationImpl>();
            server.authenticator = testAuthenticator;
            client.authenticator = testAuthenticator;
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

        [UnityTest]
        public IEnumerator NetworkClientCallsAuthenticator() => RunAsync(async () =>
        {
            Action<INetworkConnection> mockMethod = Substitute.For<Action<INetworkConnection>>();
            testAuthenticator.OnClientAuthenticated += mockMethod;

            await Task.Delay(1);

            client.ConnectHost(server);

            mockMethod.Received().Invoke(Arg.Any<INetworkConnection>());

            client.Disconnect();
        });

        [UnityTest]
        public IEnumerator NetworkServerCallsAuthenticator() => RunAsync(async () =>
        {
            Action<INetworkConnection> mockMethod = Substitute.For<Action<INetworkConnection>>();
            testAuthenticator.OnServerAuthenticated += mockMethod;

            await Task.Delay(1);

            client.ConnectHost(server);

            await Task.Delay(1);

            mockMethod.Received().Invoke(Arg.Any<INetworkConnection>());

            client.Disconnect();
        });
    }
}
