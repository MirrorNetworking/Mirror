using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverTargetRpcTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void TargetRpcValid()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void TargetRpcStartsWithTarget()
        {
            Assert.That(weaverErrors, Contains.Item("DoesntStartWithTarget must start with Target.  Consider renaming it to TargetDoesntStartWithTarget (at System.Void MirrorTest.TargetRpcStartsWithTarget::DoesntStartWithTarget(Mirror.NetworkConnection))"));
        }

        [Test]
        public void TargetRpcCantBeStatic()
        {
            Assert.That(weaverErrors, Contains.Item("TargetCantBeStatic must not be static (at System.Void MirrorTest.TargetRpcCantBeStatic::TargetCantBeStatic(Mirror.NetworkConnection))"));
        }

        [Test]
        public void SyncEventValid()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncEventStartsWithEvent()
        {
            Assert.That(weaverErrors, Contains.Item("DoCoolThingsWithExcitingPeople must start with Event.  Consider renaming it to EventDoCoolThingsWithExcitingPeople (at MirrorTest.SyncEventStartsWithEvent/MySyncEventDelegate MirrorTest.SyncEventStartsWithEvent::DoCoolThingsWithExcitingPeople)"));
        }

        [Test]
        public void SyncEventParamGeneric()
        {
            Assert.That(weaverErrors, Contains.Item("EventDoCoolThingsWithExcitingPeople must not have generic parameters.  Consider creating a new class that inherits from MirrorTest.SyncEventParamGeneric/MySyncEventDelegate`1<System.Int32> instead (at MirrorTest.SyncEventParamGeneric/MySyncEventDelegate`1<System.Int32> MirrorTest.SyncEventParamGeneric::EventDoCoolThingsWithExcitingPeople)"));
        }
    }
}
