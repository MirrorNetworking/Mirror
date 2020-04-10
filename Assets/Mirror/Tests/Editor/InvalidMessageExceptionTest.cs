using NUnit.Framework;

namespace Mirror.Tests
{
    [TestFixture]
    public class InvalidMessageExceptionTest
    {
        [Test]
        public void InvalidMessageTest()
        {
            Assert.Throws<InvalidMessageException>(() =>
            {
                throw new InvalidMessageException();
            });
        }

        [Test]
        public void InvalidMessageWithTextTest()
        {
            InvalidMessageException ex = Assert.Throws<InvalidMessageException>(() =>
            {
                throw new InvalidMessageException("Test Message");
            });

            Assert.That(ex.Message, Is.EqualTo("Test Message"));
        }

        [Test]
        public void InvalidMessageWithTextAndInnerTest()
        {
            InvalidMessageException ex = Assert.Throws<InvalidMessageException>(() =>
            {
                throw new InvalidMessageException("Test Message Too", new System.Exception());
            });

            Assert.That(ex.Message, Is.EqualTo("Test Message Too"));
            Assert.That(ex.InnerException, Is.TypeOf(typeof(System.Exception)));
        }
    }
}
