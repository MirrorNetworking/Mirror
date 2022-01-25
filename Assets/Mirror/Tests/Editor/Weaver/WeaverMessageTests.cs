using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverMessageTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void MessageMemberGeneric()
        {
            HasError("Cannot generate reader for generic variable HasGeneric`1. Use a supported type or provide a custom reader",
                "WeaverMessageTests.MessageMemberGeneric.HasGeneric`1<System.Int32>");
            HasError("invalidField has an unsupported type",
                "WeaverMessageTests.MessageMemberGeneric.HasGeneric`1<System.Int32> WeaverMessageTests.MessageMemberGeneric.MessageMemberGeneric::invalidField");
            HasError("Cannot generate writer for generic type HasGeneric`1. Use a supported type or provide a custom writer",
                "WeaverMessageTests.MessageMemberGeneric.HasGeneric`1<System.Int32>");
        }

        [Test]
        public void MessageMemberInterface()
        {
            HasError("Cannot generate reader for interface SuperCoolInterface. Use a supported type or provide a custom reader",
                "WeaverMessageTests.MessageMemberInterface.SuperCoolInterface");
            HasError("invalidField has an unsupported type",
                "WeaverMessageTests.MessageMemberInterface.SuperCoolInterface WeaverMessageTests.MessageMemberInterface.MessageMemberInterface::invalidField");
            HasError("Cannot generate writer for interface SuperCoolInterface. Use a supported type or provide a custom writer",
                "WeaverMessageTests.MessageMemberInterface.SuperCoolInterface");
        }
    }
}

