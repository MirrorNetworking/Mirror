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
        public void CommandCantBeStatic()
        {
            HasError("CmdCantBeStatic cannot be static",
                "System.Void WeaverCommandTests.CommandCantBeStatic.CommandCantBeStatic::CmdCantBeStatic()");
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
        public void CommandThatIgnoresAuthorityWithSenderConnection()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void CommandWithSenderConnectionAndOtherArgs()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void ErrorForOptionalNetworkConnectionThatIsNotSenderConnection()
        {
            HasError("CmdFunction has invalid parameter connection, Cannot pass NetworkConnections. Instead use 'NetworkConnectionToClient conn = null' to get the sender's connection on the server",
                "System.Void WeaverCommandTests.ErrorForOptionalNetworkConnectionThatIsNotSenderConnection.ErrorForOptionalNetworkConnectionThatIsNotSenderConnection::CmdFunction(Mirror.NetworkConnection)");
        }

        [Test]
        public void ErrorForNetworkConnectionThatIsNotSenderConnection()
        {
            HasError("CmdFunction has invalid parameter connection, Cannot pass NetworkConnections. Instead use 'NetworkConnectionToClient conn = null' to get the sender's connection on the server",
                "System.Void WeaverCommandTests.ErrorForNetworkConnectionThatIsNotSenderConnection.ErrorForNetworkConnectionThatIsNotSenderConnection::CmdFunction(Mirror.NetworkConnection)");
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
        public void OverrideVirtualCallBaseCommand()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void OverrideVirtualCallsBaseCommandWithMultipleBaseClasses()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void OverrideVirtualCallsBaseCommandWithOverride()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void AbstractCommand()
        {
            HasError("Abstract Commands are currently not supported, use virtual method instead",
                "System.Void WeaverCommandTests.AbstractCommand.AbstractCommand::CmdDoSomething()");
        }

        [Test]
        public void OverrideAbstractCommand()
        {
            HasError("Abstract Commands are currently not supported, use virtual method instead",
                "System.Void WeaverCommandTests.OverrideAbstractCommand.BaseBehaviour::CmdDoSomething()");
        }
    }
}
