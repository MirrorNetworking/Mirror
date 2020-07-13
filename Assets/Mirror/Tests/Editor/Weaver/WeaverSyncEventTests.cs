using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverSyncEventTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void SyncEventValid()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void MultipleSyncEvent()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void ErrorWhenSyncEventUsesGenericParameter()
        {
            Assert.That(weaverErrors, Contains.Item("EventDoCoolThingsWithExcitingPeople must not have generic parameters.  " +
                "Consider creating a new class that inherits from WeaverSyncEventTests.ErrorWhenSyncEventUsesGenericParameter.ErrorWhenSyncEventUsesGenericParameter/MySyncEventDelegate`1<System.Int32> instead " +
                "(at WeaverSyncEventTests.ErrorWhenSyncEventUsesGenericParameter.ErrorWhenSyncEventUsesGenericParameter/MySyncEventDelegate`1<System.Int32> WeaverSyncEventTests.ErrorWhenSyncEventUsesGenericParameter.ErrorWhenSyncEventUsesGenericParameter::EventDoCoolThingsWithExcitingPeople)"));
        }
    }
}
