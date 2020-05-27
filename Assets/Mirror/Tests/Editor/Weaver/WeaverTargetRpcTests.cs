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
            Assert.That(weaverErrors, Contains.Item($"TargetRpcMethod has invalid parameter nc. Cannot pass NeworkConnections " +
                "(at System.Void WeaverTargetRpcTests.ErrorWhenNetworkConnectionIsNotTheFirstParameter.ErrorWhenNetworkConnectionIsNotTheFirstParameter::TargetRpcMethod(System.Int32,Mirror.NetworkConnection))"));
        }
    }
}
