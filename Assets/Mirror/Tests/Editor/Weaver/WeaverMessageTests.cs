using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverMessageTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void MessageValid()
        {
            IsSuccess();
        }

        [Test]
        public void MessageWithBaseClass()
        {
            IsSuccess();
        }

        [Test]
        public void MessageSelfReferencing()
        {
            HasError("MessageSelfReferencing has field selfReference that references itself",
                "WeaverMessageTests.MessageSelfReferencing.MessageSelfReferencing WeaverMessageTests.MessageSelfReferencing.MessageSelfReferencing::selfReference");
        }

        [Test]
        public void MessageMemberGeneric()
        {
            HasError("Cannot generate writer for generic type HasGeneric`1. Use a supported type or provide a custom writer",
                "WeaverMessageTests.MessageMemberGeneric.HasGeneric`1<System.Int32>");
            HasError("invalidField has unsupported type",
                "WeaverMessageTests.MessageMemberGeneric.HasGeneric`1<System.Int32> WeaverMessageTests.MessageMemberGeneric.MessageMemberGeneric::invalidField");
        }

        [Test]
        public void MessageMemberInterface()
        {
            HasError("Cannot generate writer for interface SuperCoolInterface. Use a supported type or provide a custom writer",
                "WeaverMessageTests.MessageMemberInterface.SuperCoolInterface");
            HasError("invalidField has unsupported type",
                "WeaverMessageTests.MessageMemberInterface.SuperCoolInterface WeaverMessageTests.MessageMemberInterface.MessageMemberInterface::invalidField");
        }

        [Test]
        public void MessageNestedInheritance()
        {
            IsSuccess();
        }

        [Test]
        public void AbstractMessageMethods()
        {
            IsSuccess();
        }
    }
}

