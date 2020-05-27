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
        public void ErrorWhenSyncEventDoesntStartWithEvent()
        {
            Assert.That(weaverErrors, Contains.Item("DoCoolThingsWithExcitingPeople must start with Event.  " +
                "Consider renaming it to EventDoCoolThingsWithExcitingPeople " +
                "(at WeaverSyncEventTests.ErrorWhenSyncEventDoesntStartWithEvent.ErrorWhenSyncEventDoesntStartWithEvent/MySyncEventDelegate WeaverSyncEventTests.ErrorWhenSyncEventDoesntStartWithEvent.ErrorWhenSyncEventDoesntStartWithEvent::DoCoolThingsWithExcitingPeople)"));
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
