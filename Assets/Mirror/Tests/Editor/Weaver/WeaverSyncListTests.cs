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

        [Test]
        public void SyncListErrorWhenUsingGenericListInNetworkBehaviour()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            string weaverError = @"Mirror\.Weaver error:";
            string type = @"MirrorTest\.SomeList`1<System\.Int32> MirrorTest.SyncListErrorWhenUsingGenericListInNetworkBehaviour::someList";
            string errorMessage = @"Can not use generic SyncObjects directly in NetworkBehaviour\. Create a class and inherit from the generic syncList instead\.";
            Assert.That(weaverErrors, Has.Some.Match($"{weaverError} {type} {errorMessage}"));
        }
    }
}
