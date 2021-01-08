using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverServerRpcTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void ServerRpcValid()
        {
            Assert.That(weaverLog.errors, Is.Empty);
        }

        [Test]
        public void ServerRpcCantBeStatic()
        {
            Assert.That(weaverLog.errors, Contains.Item("CmdCantBeStatic must not be static (at System.Void WeaverServerRpcTests.ServerRpcCantBeStatic.ServerRpcCantBeStatic::CmdCantBeStatic())"));
        }

        [Test]
        public void ServerRpcThatIgnoresAuthority()
        {
            Assert.That(weaverLog.errors, Is.Empty);
        }

        [Test]
        public void ServerRpcWithArguments()
        {
            Assert.That(weaverLog.errors, Is.Empty);
        }

        [Test]
        public void ServerRpcThatIgnoresAuthorityWithSenderConnection()
        {
            Assert.That(weaverLog.errors, Is.Empty);
        }

        [Test]
        public void ServerRpcWithSenderConnectionAndOtherArgs()
        {
            Assert.That(weaverLog.errors, Is.Empty);
        }

        [Test]
        public void VirtualServerRpc()
        {
            Assert.That(weaverLog.errors, Is.Empty);
        }

        [Test]
        public void OverrideVirtualServerRpc()
        {
            Assert.That(weaverLog.errors, Is.Empty);
        }

        [Test]
        public void OverrideVirtualCallBaseServerRpc()
        {
            Assert.That(weaverLog.errors, Is.Empty);
        }

        [Test]
        public void OverrideVirtualCallsBaseServerRpcWithMultipleBaseClasses()
        {
            Assert.That(weaverLog.errors, Is.Empty);
        }

        [Test]
        public void OverrideVirtualCallsBaseServerRpcWithOverride()
        {
            Assert.That(weaverLog.errors, Is.Empty);
        }

        [Test]
        public void AbstractServerRpc()
        {
            Assert.That(weaverLog.errors, Contains.Item("Abstract Rpcs are currently not supported, use virtual method instead (at System.Void WeaverServerRpcTests.AbstractServerRpc.AbstractServerRpc::CmdDoSomething())"));
        }

        [Test]
        public void OverrideAbstractServerRpc()
        {
            Assert.That(weaverLog.errors, Contains.Item("Abstract Rpcs are currently not supported, use virtual method instead (at System.Void WeaverServerRpcTests.OverrideAbstractServerRpc.BaseBehaviour::CmdDoSomething())"));
        }

        [Test]
        public void ServerRpcWithReturn()
        {
            Assert.That(weaverLog.errors, Is.Empty);
        }
    }
}
