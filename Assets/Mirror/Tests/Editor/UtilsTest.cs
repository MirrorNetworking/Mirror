using NUnit.Framework;

namespace Mirror.Tests
{
    public class UtilsTest
    {
        [Test]
        public void ParseHostAndPort()
        {
            // just IP. can't split.
            bool result = Utils.ParseHostAndPort("127.0.0.1", out string _, out ushort _);
            Assert.That(result, Is.False);

            // just Host. can't split.
            result = Utils.ParseHostAndPort("mirror-networking.com", out string _, out ushort _);
            Assert.That(result, Is.False);

            // IP & Port
            result = Utils.ParseHostAndPort("127.0.0.1:42", out string host, out ushort port);
            Assert.That(result, Is.True);
            Assert.That(host, Is.EqualTo("127.0.0.1"));
            Assert.That(port, Is.EqualTo(42));

            // Hostname & Port
            result = Utils.ParseHostAndPort("mirror-networking.com:42", out host, out port);
            Assert.That(result, Is.True);
            Assert.That(host, Is.EqualTo("mirror-networking.com"));
            Assert.That(port, Is.EqualTo(42));
        }
    }
}
