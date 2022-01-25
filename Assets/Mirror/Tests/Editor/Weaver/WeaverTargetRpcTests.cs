using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverTargetRpcTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void ErrorWhenTargetRpcIsStatic()
        {
            HasError("TargetCantBeStatic must not be static",
                "System.Void WeaverTargetRpcTests.ErrorWhenTargetRpcIsStatic.ErrorWhenTargetRpcIsStatic::TargetCantBeStatic(Mirror.NetworkConnection)");
        }

        [Test]
        public void ErrorWhenNetworkConnectionIsNotTheFirstParameter()
        {
            HasError("TargetRpcMethod has invalid parameter nc. Cannot pass NetworkConnections",
                "System.Void WeaverTargetRpcTests.ErrorWhenNetworkConnectionIsNotTheFirstParameter.ErrorWhenNetworkConnectionIsNotTheFirstParameter::TargetRpcMethod(System.Int32,Mirror.NetworkConnection)");
        }

        [Test]
        public void AbstractTargetRpc()
        {
            HasError("Abstract TargetRpc are currently not supported, use virtual method instead",
                "System.Void WeaverTargetRpcTests.AbstractTargetRpc.AbstractTargetRpc::TargetDoSomething()");
        }

        [Test]
        public void OverrideAbstractTargetRpc()
        {
            HasError("Abstract TargetRpc are currently not supported, use virtual method instead",
                "System.Void WeaverTargetRpcTests.OverrideAbstractTargetRpc.BaseBehaviour::TargetDoSomething()");
        }
    }
}
