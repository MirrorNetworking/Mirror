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
        public void MessageWithBaseClass()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void MessageSelfReferencing()
        {
            Assert.That(weaverErrors, Contains.Item("MessageSelfReferencing has field selfReference that references itself (at WeaverMessageTests.MessageSelfReferencing.MessageSelfReferencing WeaverMessageTests.MessageSelfReferencing.MessageSelfReferencing::selfReference)"));
        }

        [Test]
        public void MessageMemberGeneric()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for generic type HasGeneric`1. Use a supported type or provide a custom writer (at WeaverMessageTests.MessageMemberGeneric.HasGeneric`1<System.Int32>)"));
            Assert.That(weaverErrors, Contains.Item("invalidField has unsupported type (at WeaverMessageTests.MessageMemberGeneric.HasGeneric`1<System.Int32> WeaverMessageTests.MessageMemberGeneric.MessageMemberGeneric::invalidField)"));
        }

        [Test]
        public void MessageMemberInterface()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for interface SuperCoolInterface. Use a supported type or provide a custom writer (at WeaverMessageTests.MessageMemberInterface.SuperCoolInterface)"));
            Assert.That(weaverErrors, Contains.Item("invalidField has unsupported type (at WeaverMessageTests.MessageMemberInterface.SuperCoolInterface WeaverMessageTests.MessageMemberInterface.MessageMemberInterface::invalidField)"));
        }
    }
}
