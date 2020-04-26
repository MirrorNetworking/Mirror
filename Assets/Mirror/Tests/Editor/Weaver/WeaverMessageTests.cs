using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverMessageTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void MessageValid()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void MessageSelfReferencing()
        {
            Assert.That(weaverErrors, Contains.Item("MessageSelfReferencing has field selfReference that references itself (at MirrorTest.MessageSelfReferencing MirrorTest.MessageSelfReferencing::selfReference)"));        }

        [Test]
        public void MessageMemberGeneric()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for generic type HasGeneric`1. Use a supported type or provide a custom writer (at MirrorTest.HasGeneric`1<System.Int32>)"));
            Assert.That(weaverErrors, Contains.Item("invalidField has unsupported type (at MirrorTest.HasGeneric`1<System.Int32> MirrorTest.MessageMemberGeneric::invalidField)"));
        }

        [Test]
        public void MessageMemberInterface()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for interface SuperCoolInterface. Use a supported type or provide a custom writer (at MirrorTest.SuperCoolInterface)"));
            Assert.That(weaverErrors, Contains.Item("invalidField has unsupported type (at MirrorTest.SuperCoolInterface MirrorTest.MessageMemberInterface::invalidField)"));
        }
    }
}
