using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverCommandTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void CommandValid()
        {
            IsSuccess();
        }

        [Test]
        public void CommandCantBeStatic()
        {
            HasError("CmdCantBeStatic must not be static",
                "System.Void WeaverCommandTests.CommandCantBeStatic.CommandCantBeStatic::CmdCantBeStatic()");
        }

        [Test]
        public void CommandThatIgnoresAuthority()
        {
            IsSuccess();
        }

        [Test]
        public void CommandWithArguments()
        {
            IsSuccess();
        }

        [Test]
        public void CommandThatIgnoresAuthorityWithSenderConnection()
        {
            IsSuccess();
        }

        [Test]
        public void CommandWithSenderConnectionAndOtherArgs()
        {
            IsSuccess();
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
            IsSuccess();
        }

        [Test]
        public void OverrideVirtualCommand()
        {
            IsSuccess();
        }

        [Test]
        public void OverrideVirtualCallBaseCommand()
        {
            IsSuccess();
        }

        [Test]
        public void OverrideVirtualCallsBaseCommandWithMultipleBaseClasses()
        {
            IsSuccess();
        }

        [Test]
        public void OverrideVirtualCallsBaseCommandWithOverride()
        {
            IsSuccess();
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
