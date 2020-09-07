using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    // Some tests for SyncObjects are in WeaverSyncListTests and apply to SyncDictionary too
    public class WeaverSyncSetTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void SyncSet()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncSetByteValid()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncSetGenericAbstractInheritance()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncSetGenericInheritance()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncSetInheritance()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncSetStruct()
        {
            Assert.That(weaverErrors, Is.Empty);
        }
    }
}
