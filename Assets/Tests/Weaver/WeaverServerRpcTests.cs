using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverServerRpcTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void ServerRpcValid()
        {
            IsSuccess();
        }

        [Test]
        public void ServerRpcCantBeStatic()
        {
            HasError("CmdCantBeStatic must not be static","System.Void WeaverServerRpcTests.ServerRpcCantBeStatic.ServerRpcCantBeStatic::CmdCantBeStatic()");
        }

        [Test]
        public void ServerRpcThatIgnoresAuthority()
        {
            IsSuccess();
        }

        [Test]
        public void ServerRpcWithArguments()
        {
            IsSuccess();
        }

        [Test]
        public void ServerRpcThatIgnoresAuthorityWithSenderConnection()
        {
            IsSuccess();
        }

        [Test]
        public void ServerRpcWithSenderConnectionAndOtherArgs()
        {
            IsSuccess();
        }

        [Test]
        public void VirtualServerRpc()
        {
            IsSuccess();
        }

        [Test]
        public void OverrideVirtualServerRpc()
        {
            IsSuccess();
        }

        [Test]
        public void OverrideVirtualCallBaseServerRpc()
        {
            IsSuccess();
        }

        [Test]
        public void OverrideVirtualCallsBaseServerRpcWithMultipleBaseClasses()
        {
            IsSuccess();
        }

        [Test]
        public void OverrideVirtualCallsBaseServerRpcWithOverride()
        {
            IsSuccess();
        }

        [Test]
        public void AbstractServerRpc()
        {
            HasError("Abstract Rpcs are currently not supported, use virtual method instead","System.Void WeaverServerRpcTests.AbstractServerRpc.AbstractServerRpc::CmdDoSomething()");
        }

        [Test]
        public void OverrideAbstractServerRpc()
        {
            HasError("Abstract Rpcs are currently not supported, use virtual method instead","System.Void WeaverServerRpcTests.OverrideAbstractServerRpc.BaseBehaviour::CmdDoSomething()");
        }

        [Test]
        public void ServerRpcWithReturn()
        {
            IsSuccess();
        }
    }
}
