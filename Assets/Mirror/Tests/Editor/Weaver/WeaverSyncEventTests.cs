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
        public void SyncEventStartsWithEvent()
        {
            Assert.That(weaverErrors, Contains.Item("DoCoolThingsWithExcitingPeople must start with Event. Consider renaming it to EventDoCoolThingsWithExcitingPeople " +
                "(at WeaverTargetRpcTests.SyncEventStartsWithEvent.SyncEventStartsWithEvent/MySyncEventDelegate WeaverTargetRpcTests.SyncEventStartsWithEvent.SyncEventStartsWithEvent::DoCoolThingsWithExcitingPeople)"));
        }

        [Test]
        public void SyncEventParamGeneric()
        {
            Assert.That(weaverErrors, Contains.Item("EventDoCoolThingsWithExcitingPeople must not have generic parameters. " +
                "Consider creating a new class that inherits from WeaverTargetRpcTests.SyncEventParamGeneric.SyncEventParamGeneric/MySyncEventDelegate`1<System.Int32> instead " +
                "(at WeaverTargetRpcTests.SyncEventParamGeneric.SyncEventParamGeneric/MySyncEventDelegate`1<System.Int32> WeaverTargetRpcTests.SyncEventParamGeneric.SyncEventParamGeneric::EventDoCoolThingsWithExcitingPeople)"));
        }
    }
}
