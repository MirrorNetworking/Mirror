using NUnit.Framework;

namespace Mirror.Tests
{
    [TestFixture]
    public class NetworkManagerHudTest : NetworkManagerHUD
    {
        [Test]
        public void VariablesTest()
        {
            Assert.That(showGUI, Is.True);
            Assert.That(serverIp, Is.EqualTo("localhost"));
        }
    }
}
