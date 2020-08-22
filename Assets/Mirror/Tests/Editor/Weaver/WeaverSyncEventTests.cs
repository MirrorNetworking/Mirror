using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverSyncEventTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void SyncEventValid()
        {
            IsSuccess();
        }

        [Test]
        public void MultipleSyncEvent()
        {
            IsSuccess();
        }

        [Test]
        public void ErrorWhenSyncEventUsesGenericParameter()
        {
            HasError("EventDoCoolThingsWithExcitingPeople must not have generic parameters.  " +
                "Consider creating a new class that inherits from WeaverSyncEventTests.ErrorWhenSyncEventUsesGenericParameter.ErrorWhenSyncEventUsesGenericParameter/MySyncEventDelegate`1<System.Int32> instead",
                "WeaverSyncEventTests.ErrorWhenSyncEventUsesGenericParameter.ErrorWhenSyncEventUsesGenericParameter/MySyncEventDelegate`1<System.Int32> WeaverSyncEventTests.ErrorWhenSyncEventUsesGenericParameter.ErrorWhenSyncEventUsesGenericParameter::EventDoCoolThingsWithExcitingPeople");
        }
    }
}
