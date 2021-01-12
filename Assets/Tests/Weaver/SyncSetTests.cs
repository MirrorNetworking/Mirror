using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    // Some tests for SyncObjects are in SyncListTests and apply to SyncDictionary too
    public class SyncSetTests : TestsBuildFromTestName
    {
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
