using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverCommandTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void CommandCantBeStatic()
        {
            HasError("CmdCantBeStatic must not be static",
                "System.Void WeaverCommandTests.CommandCantBeStatic.CommandCantBeStatic::CmdCantBeStatic()");
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
