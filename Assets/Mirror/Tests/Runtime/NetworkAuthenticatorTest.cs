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
        int count;

        [SetUp]
        public void SetupTest()
        {
            gameObject = new GameObject();

            client = gameObject.AddComponent<NetworkClient>();
            server = gameObject.AddComponent<NetworkServer>();
            testAuthenticator = gameObject.AddComponent<NetworkAuthenticator>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(gameObject);
            count = 0;
        }

        
        void InvokedMethod(INetworkConnection conn)
        {
            count++;
        }

        [Test]
        public void OnServerAuthenticateTest()
        {
            testAuthenticator.OnServerAuthenticated += InvokedMethod;

            testAuthenticator.OnServerAuthenticate(Substitute.For<INetworkConnection>());

            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void OnServerAuthenticateInternalTest()
        {
            testAuthenticator.OnServerAuthenticated += InvokedMethod;

            testAuthenticator.OnServerAuthenticateInternal(Substitute.For<INetworkConnection>());

            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void OnClientAuthenticateTest()
        {
            testAuthenticator.OnClientAuthenticated += InvokedMethod;

            testAuthenticator.OnClientAuthenticate(Substitute.For<INetworkConnection>());

            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void OnClientAuthenticateInternalTest()
        {
            testAuthenticator.OnClientAuthenticated += InvokedMethod;

            testAuthenticator.OnClientAuthenticateInternal(Substitute.For<INetworkConnection>());

            Assert.That(count, Is.EqualTo(1));
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
