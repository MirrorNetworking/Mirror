using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    // Some tests for SyncObjects are in WeaverSyncListTests and apply to SyncDictionary too
    public class WeaverSyncSetTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void SyncSet()
        {
            IsSuccess();
        }

        [Test]
        public void SyncSetByteValid()
        {
            IsSuccess();
        }

        [Test]
        public void SyncSetGenericAbstractInheritance()
        {
            IsSuccess();
        }

        [Test]
        public void SyncSetGenericInheritance()
        {
            IsSuccess();
        }

        [Test]
        public void SyncSetInheritance()
        {
            IsSuccess();
        }

        [Test]
        public void SyncSetStruct()
        {
            IsSuccess();
        }
    }
}
