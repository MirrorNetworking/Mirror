using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverFuseTests
    {
        [Test]
        public void WeavingSucceded()
        {
            // .State returns false by default.
            // Weaver overwrites this to true.
            Assert.That(WeaverFuse.Weaved(), Is.True);
        }
    }
}
