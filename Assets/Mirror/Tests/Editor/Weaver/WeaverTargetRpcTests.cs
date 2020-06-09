using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverTargetRpcTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void TargetRpcValid()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void ErrorWhenMethodDoesNotStartWithTarget()
        {
            Assert.That(weaverErrors, Contains.Item("DoesntStartWithTarget must start with Target.  Consider renaming it to TargetDoesntStartWithTarget " +
                "(at System.Void WeaverTargetRpcTests.ErrorWhenMethodDoesNotStartWithTarget.ErrorWhenMethodDoesNotStartWithTarget::DoesntStartWithTarget(Mirror.NetworkConnection))"));
        }

        [Test]
        public void ErrorWhenTargetRpcIsStatic()
        {
            Assert.That(weaverErrors, Contains.Item("TargetCantBeStatic must not be static " +
                "(at System.Void WeaverTargetRpcTests.ErrorWhenTargetRpcIsStatic.ErrorWhenTargetRpcIsStatic::TargetCantBeStatic(Mirror.NetworkConnection))"));
        }

        [Test]
        public void TargetRpcCanSkipNetworkConnection()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void TargetRpcCanHaveOtherParametersWhileSkipingNetworkConnection()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void ErrorWhenNetworkConnectionIsNotTheFirstParameter()
        {
            Assert.That(weaverErrors, Contains.Item($"TargetRpcMethod has invalid parameter nc. Cannot pass NetworkConnections " +
                "(at System.Void WeaverTargetRpcTests.ErrorWhenNetworkConnectionIsNotTheFirstParameter.ErrorWhenNetworkConnectionIsNotTheFirstParameter::TargetRpcMethod(System.Int32,Mirror.NetworkConnection))"));
        }

        [Test]
        public void VirtualTargetRpc()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void OverrideVirtualTargetRpc()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void AbstractTargetRpc()
        {
            Assert.That(weaverErrors, Contains.Item("Abstract TargetRpc are currently not supported, use virtual method instead (at System.Void WeaverTargetRpcTests.AbstractTargetRpc.AbstractTargetRpc::TargetDoSomething())"));
        }

        [Test]
        public void OverrideAbstractTargetRpc()
        {
            Assert.That(weaverErrors, Contains.Item("Abstract TargetRpc are currently not supported, use virtual method instead (at System.Void WeaverTargetRpcTests.OverrideAbstractTargetRpc.BaseBehaviour::TargetDoSomething())"));
        }
    }
}
