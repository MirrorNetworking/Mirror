using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverSyncObjectsTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void SyncObjectsExactlyMax()
        {
            IsSuccess();
        }

        [Test]
        public void SyncObjectsMoreThanMax()
        {
            HasError("SyncObjectsMoreThanMax has > 64 SyncObjects (SyncLists etc). Consider refactoring your class into multiple components",
                "WeaverSyncObjectsTest.SyncObjectsMoreThanMax.SyncObjectsMoreThanMax");
        }
    }
}
