using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverSyncObjectsTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void SyncObjectsMoreThanMax()
        {
            HasError("SyncObjectsMoreThanMax has > 64 SyncObjects (SyncLists etc). Consider refactoring your class into multiple components",
                "WeaverSyncObjectsTest.SyncObjectsMoreThanMax.SyncObjectsMoreThanMax");
        }

        [Test]
        public void RecommendsReadonly()
        {
            HasWarning("list should have a 'readonly' keyword in front of the variable because Mirror.SyncObjects always need to be initialized by the Weaver.",
                "Mirror.SyncList`1<System.Int32> WeaverSyncObjectsTest.SyncObjectsMoreThanMax.RecommendsReadonly::list");
        }
    }
}
