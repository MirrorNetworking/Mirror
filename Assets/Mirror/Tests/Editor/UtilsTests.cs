using NUnit.Framework;

namespace Mirror.Tests
{
    public class UtilsTests
    {
        [Test]
        public void GetTrueRandomUInt()
        {
            uint first = Utils.GetTrueRandomUInt();
            uint second = Utils.GetTrueRandomUInt();
            Assert.That(first, !Is.EqualTo(second));
        }
    }
}
