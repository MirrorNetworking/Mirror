using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverMessageTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void MessageValid()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void MessageSelfReferencing()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: MirrorTest.PrefabClone has field $MirrorTest.PrefabClone MirrorTest.PrefabClone::selfReference that references itself"));
        }

        [Test]
        public void MessageMemberGeneric()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: Cannot generate writer for generic type MirrorTest.HasGeneric`1<System.Int32>. Use a concrete type or provide a custom writer"));
        }

        [Test]
        public void MessageMemberInterface()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: Cannot generate writer for interface MirrorTest.SuperCoolInterface. Use a concrete type or provide a custom writer"));
        }
    }
}
