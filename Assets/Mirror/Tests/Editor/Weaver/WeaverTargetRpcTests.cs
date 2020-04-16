using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverTargetRpcTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void TargetRpcValid()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void TargetRpcStartsWithTarget()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Void MirrorTest.MirrorTestPlayer::DoesntStartWithTarget(Mirror.NetworkConnection) must start with Target.  Consider renaming it to TargetDoesntStartWithTarget"));
        }

        [Test]
        public void TargetRpcCantBeStatic()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Void MirrorTest.MirrorTestPlayer::TargetCantBeStatic(Mirror.NetworkConnection) must not be static"));
        }

        [Test]
        public void SyncEventValid()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncEventStartsWithEvent()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: MirrorTest.MirrorTestPlayer/MySyncEventDelegate MirrorTest.MirrorTestPlayer::DoCoolThingsWithExcitingPeople must start with Event.  Consider renaming it to EventDoCoolThingsWithExcitingPeople"));
        }

        [Test]
        public void SyncEventParamGeneric()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: MirrorTest.MirrorTestPlayer/MySyncEventDelegate`1<System.Int32> MirrorTest.MirrorTestPlayer::EventDoCoolThingsWithExcitingPeople must not have generic parameters.  Consider creating a new class that inherits from MirrorTest.MirrorTestPlayer/MySyncEventDelegate`1<System.Int32> instead"));
        }
    }
}
