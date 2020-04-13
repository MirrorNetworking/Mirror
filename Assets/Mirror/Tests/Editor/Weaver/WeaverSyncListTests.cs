using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverSyncListTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void SyncListValid()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void SyncListMissingParamlessCtor()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: MirrorTest.MirrorTestPlayer/SyncListString2 MirrorTest.MirrorTestPlayer::Foo does not have a default constructor"));
        }

        [Test]
        public void SyncListByteValid()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }
    }
}
