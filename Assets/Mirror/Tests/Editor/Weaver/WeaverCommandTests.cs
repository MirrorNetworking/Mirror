using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverCommandTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void CommandValid()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void CommandStartsWithCmd()
        {
            Assert.That(weaverErrors, Contains.Item("DoesntStartWithCmd must start with Cmd.  Consider renaming it to CmdDoesntStartWithCmd (at System.Void MirrorTest.CommandStartsWithCmd::DoesntStartWithCmd())"));
        }

        [Test]
        public void CommandCantBeStatic()
        {
            Assert.That(weaverErrors, Contains.Item("CmdCantBeStatic cannot be static (at System.Void MirrorTest.CommandCantBeStatic::CmdCantBeStatic())"));
        }
    }
}
