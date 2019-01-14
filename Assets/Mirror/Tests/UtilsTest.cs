using NUnit.Framework;
namespace Mirror
{
    [TestFixture]
    public class UtilsTest
    {
        [Test]
        public void TestScaleFloatToByte()
        {
            Assert.That(Utils.ScaleFloatToByte( -1f, -1f, 1f, byte.MinValue, byte.MaxValue), Is.EqualTo(0));
            Assert.That(Utils.ScaleFloatToByte(  0f, -1f, 1f, byte.MinValue, byte.MaxValue), Is.EqualTo(127));
            Assert.That(Utils.ScaleFloatToByte(0.5f, -1f, 1f, byte.MinValue, byte.MaxValue), Is.EqualTo(191));
            Assert.That(Utils.ScaleFloatToByte(  1f, -1f, 1f, byte.MinValue, byte.MaxValue), Is.EqualTo(255));
        }

        [Test]
        public void ScaleByteToFloat()
        {
            Assert.That(Utils.ScaleByteToFloat(  0, byte.MinValue, byte.MaxValue, -1, 1), Is.EqualTo(-1).Within(0.0001f));
            Assert.That(Utils.ScaleByteToFloat(127, byte.MinValue, byte.MaxValue, -1, 1), Is.EqualTo(-0.003921569f).Within(0.0001f));
            Assert.That(Utils.ScaleByteToFloat(191, byte.MinValue, byte.MaxValue, -1, 1), Is.EqualTo(0.4980392f).Within(0.0001f));
            Assert.That(Utils.ScaleByteToFloat(255, byte.MinValue, byte.MaxValue, -1, 1), Is.EqualTo(1).Within(0.0001f));
        }
    }
}