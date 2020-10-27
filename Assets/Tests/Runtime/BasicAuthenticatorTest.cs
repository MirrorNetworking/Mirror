using Mirror.Authenticators;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class BasicAuthenticatorTest : ClientServerSetup<MockComponent>
    {

        BasicAuthenticator authenticator;

        [Test]
        public void CheckConnected()
        {
            // Should have connected
            Assert.That(connectionToServer, Is.Not.Null);
            Assert.That(connectionToClient, Is.Not.Null);
        }

        public override void ExtraSetup()
        {
            authenticator = server.gameObject.AddComponent<BasicAuthenticator>();

            server.authenticator = authenticator;
            client.authenticator = authenticator;

            base.ExtraSetup();
        }
    }
}
