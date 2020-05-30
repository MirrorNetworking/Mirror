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
            Assert.That(weaverErrors, Contains.Item("DoesntStartWithCmd must start with Cmd.  Consider renaming it to CmdDoesntStartWithCmd (at System.Void WeaverCommandTests.CommandStartsWithCmd.CommandStartsWithCmd::DoesntStartWithCmd())"));
        }

        [Test]
        public void CommandCantBeStatic()
        {
            Assert.That(weaverErrors, Contains.Item("CmdCantBeStatic cannot be static (at System.Void WeaverCommandTests.CommandCantBeStatic.CommandCantBeStatic::CmdCantBeStatic())"));
        }

        [Test]
        public void CommandThatIgnoresAuthority()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void CommandWithArguments()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void VirtualCommand()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void OverrideVirtualCommand()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void AbstractCommand()
        {
            Assert.That(weaverErrors, Contains.Item("Abstract Commands are currently not supported, use virtual method instead (at System.Void WeaverCommandTests.AbstractCommand.AbstractCommand::CmdDoSomething())"));
        }

        [Test]
        public void OverrideAbstractCommand()
        {
            Assert.That(weaverErrors, Contains.Item("Abstract Commands are currently not supported, use virtual method instead (at System.Void WeaverCommandTests.OverrideAbstractCommand.BaseBehaviour::CmdDoSomething())"));
        }
    }
}
