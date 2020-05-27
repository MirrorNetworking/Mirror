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
        public void ErrorWhenTargetRpcIsMissingNetworkConnection()
        {
            Assert.That(weaverErrors, Contains.Item("TargetRpcMethod must have NetworkConnection as the first parameter " +
                "(at System.Void WeaverTargetRpcTests.ErrorWhenTargetRpcIsMissingNetworkConnection.ErrorWhenTargetRpcIsMissingNetworkConnection::TargetRpcMethod())"));
        }

        [Test]
        public void ErrorWhenNetworkConnectionIsNotTheFirstParameter()
        {
            Assert.That(weaverErrors, Contains.Item("TargetRpcMethod must have NetworkConnection as the first parameter " +
                "(at System.Void WeaverTargetRpcTests.ErrorWhenNetworkConnectionIsNotTheFirstParameter.ErrorWhenNetworkConnectionIsNotTheFirstParameter::TargetRpcMethod(System.Int32,Mirror.NetworkConnection))"));
        }
    }
}
