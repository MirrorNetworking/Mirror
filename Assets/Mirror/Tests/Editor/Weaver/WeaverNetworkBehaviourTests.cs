using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverNetworkBehaviourTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void NetworkBehaviourValid()
        {
            IsSuccess();
        }

        [Test]
        public void NetworkBehaviourAbstractBaseValid()
        {
            IsSuccess();
        }

        [Test]
        public void NetworkBehaviourGeneric()
        {
            HasError("NetworkBehaviourGeneric`1 cannot have generic parameters",
                "WeaverNetworkBehaviourTests.NetworkBehaviourGeneric.NetworkBehaviourGeneric`1");
        }

        [Test]
        public void NetworkBehaviourCmdGenericParam()
        {
            HasError("CmdCantHaveGeneric cannot have generic parameters",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourCmdGenericParam.NetworkBehaviourCmdGenericParam::CmdCantHaveGeneric()");
        }

        [Test]
        public void NetworkBehaviourCmdCoroutine()
        {
            HasError("CmdCantHaveCoroutine cannot be a coroutine",
                "System.Collections.IEnumerator WeaverNetworkBehaviourTests.NetworkBehaviourCmdCoroutine.NetworkBehaviourCmdCoroutine::CmdCantHaveCoroutine()");
        }

        [Test]
        public void NetworkBehaviourCmdVoidReturn()
        {
            HasError("CmdCantHaveNonVoidReturn cannot return a value.  Make it void instead",
                "System.Int32 WeaverNetworkBehaviourTests.NetworkBehaviourCmdVoidReturn.NetworkBehaviourCmdVoidReturn::CmdCantHaveNonVoidReturn()");
        }

        [Test]
        public void NetworkBehaviourTargetRpcGenericParam()
        {
            HasError("TargetRpcCantHaveGeneric cannot have generic parameters",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcGenericParam.NetworkBehaviourTargetRpcGenericParam::TargetRpcCantHaveGeneric()");
        }

        [Test]
        public void NetworkBehaviourTargetRpcCoroutine()
        {
            HasError("TargetRpcCantHaveCoroutine cannot be a coroutine",
                "System.Collections.IEnumerator WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcCoroutine.NetworkBehaviourTargetRpcCoroutine::TargetRpcCantHaveCoroutine()");
        }

        [Test]
        public void NetworkBehaviourTargetRpcVoidReturn()
        {
            HasError("TargetRpcCantHaveNonVoidReturn cannot return a value.  Make it void instead",
                "System.Int32 WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcVoidReturn.NetworkBehaviourTargetRpcVoidReturn::TargetRpcCantHaveNonVoidReturn()");
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamOut()
        {
            HasError("TargetRpcCantHaveParamOut cannot have out parameters",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamOut.NetworkBehaviourTargetRpcParamOut::TargetRpcCantHaveParamOut(Mirror.NetworkConnection,System.Int32&)");
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamOptional()
        {
            HasError("TargetRpcCantHaveParamOptional cannot have optional parameters",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamOptional.NetworkBehaviourTargetRpcParamOptional::TargetRpcCantHaveParamOptional(Mirror.NetworkConnection,System.Int32)");
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamRef()
        {
            HasError("Cannot pass Int32& by reference",
                "System.Int32&");
            HasError("TargetRpcCantHaveParamRef has invalid parameter monkeys",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamRef.NetworkBehaviourTargetRpcParamRef::TargetRpcCantHaveParamRef(Mirror.NetworkConnection,System.Int32&)");
            HasError("Cannot pass type Int32& by reference",
                "System.Int32&");
            HasError("TargetRpcCantHaveParamRef has invalid parameter monkeys.  Unsupported type System.Int32&,  use a supported Mirror type instead",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamRef.NetworkBehaviourTargetRpcParamRef::TargetRpcCantHaveParamRef(Mirror.NetworkConnection,System.Int32&)");
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamAbstract()
        {
            HasError("Cannot generate writer for abstract class AbstractClass. Use a supported type or provide a custom writer",
                "WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamAbstract.NetworkBehaviourTargetRpcParamAbstract/AbstractClass");
            // TODO change weaver to run checks for write/read at the same time
            //HasError("AbstractClass can't be deserialized because it has no default constructor",
            //    "WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamAbstract.NetworkBehaviourTargetRpcParamAbstract/AbstractClass");
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamComponent()
        {
            HasError("Cannot generate writer for component type ComponentClass. Use a supported type or provide a custom writer",
                "WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamComponent.NetworkBehaviourTargetRpcParamComponent/ComponentClass");
            HasError("TargetRpcCantHaveParamComponent has invalid parameter monkeyComp",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamComponent.NetworkBehaviourTargetRpcParamComponent::TargetRpcCantHaveParamComponent(Mirror.NetworkConnection,WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamComponent.NetworkBehaviourTargetRpcParamComponent/ComponentClass)");
            HasError("Cannot generate reader for component type ComponentClass. Use a supported type or provide a custom reader",
                "WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamComponent.NetworkBehaviourTargetRpcParamComponent/ComponentClass");
            HasError("TargetRpcCantHaveParamComponent has invalid parameter monkeyComp.  Unsupported type WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamComponent.NetworkBehaviourTargetRpcParamComponent/ComponentClass,  use a supported Mirror type instead",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamComponent.NetworkBehaviourTargetRpcParamComponent::TargetRpcCantHaveParamComponent(Mirror.NetworkConnection,WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamComponent.NetworkBehaviourTargetRpcParamComponent/ComponentClass)");
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamNetworkConnection()
        {
            IsSuccess();
        }

        [Test]
        public void NetworkBehaviourTargetRpcDuplicateName()
        {
            HasError("Duplicate Target Rpc name TargetRpcCantHaveSameName",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcDuplicateName.NetworkBehaviourTargetRpcDuplicateName::TargetRpcCantHaveSameName(Mirror.NetworkConnection,System.Int32,System.Int32)");
        }

        [Test]
        public void NetworkBehaviourClientRpcGenericParam()
        {
            HasError("RpcCantHaveGeneric cannot have generic parameters",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcGenericParam.NetworkBehaviourClientRpcGenericParam::RpcCantHaveGeneric()");
        }

        [Test]
        public void NetworkBehaviourClientRpcCoroutine()
        {
            HasError("RpcCantHaveCoroutine cannot be a coroutine",
                "System.Collections.IEnumerator WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcCoroutine.NetworkBehaviourClientRpcCoroutine::RpcCantHaveCoroutine()");
        }

        [Test]
        public void NetworkBehaviourClientRpcVoidReturn()
        {
            HasError("RpcCantHaveNonVoidReturn cannot return a value.  Make it void instead",
                "System.Int32 WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcVoidReturn.NetworkBehaviourClientRpcVoidReturn::RpcCantHaveNonVoidReturn()");
        }

        [Test]
        public void NetworkBehaviourClientRpcParamOut()
        {
            HasError("RpcCantHaveParamOut cannot have out parameters",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamOut.NetworkBehaviourClientRpcParamOut::RpcCantHaveParamOut(System.Int32&)");
        }

        [Test]
        public void NetworkBehaviourClientRpcParamOptional()
        {
            HasError("RpcCantHaveParamOptional cannot have optional parameters",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamOptional.NetworkBehaviourClientRpcParamOptional::RpcCantHaveParamOptional(System.Int32)");
        }

        [Test]
        public void NetworkBehaviourClientRpcParamRef()
        {
            HasError("Cannot pass Int32& by reference",
                "System.Int32&");
            HasError("RpcCantHaveParamRef has invalid parameter monkeys",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamRef.NetworkBehaviourClientRpcParamRef::RpcCantHaveParamRef(System.Int32&)");
            // TODO change weaver to run checks for write/read at the same time
            //HasError("Cannot pass type Int32& by reference",
            //    "System.Int32&");
            //HasError("RpcCantHaveParamRef has invalid parameter monkeys.  Unsupported type System.Int32&,  use a supported Mirror type instead",
            //    "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamRef.NetworkBehaviourClientRpcParamRef::RpcCantHaveParamRef(System.Int32&)");
        }

        [Test]
        public void NetworkBehaviourClientRpcParamAbstract()
        {
            HasError("Cannot generate writer for abstract class AbstractClass. Use a supported type or provide a custom writer",
                "WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamAbstract.NetworkBehaviourClientRpcParamAbstract/AbstractClass");
            // TODO change weaver to run checks for write/read at the same time
            //HasError("AbstractClass can't be deserialized because it has no default constructor",
            //    "WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamAbstract.NetworkBehaviourClientRpcParamAbstract/AbstractClass");
        }

        [Test]
        public void NetworkBehaviourClientRpcParamComponent()
        {
            HasError("Cannot generate writer for component type ComponentClass. Use a supported type or provide a custom writer",
                "WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamComponent.NetworkBehaviourClientRpcParamComponent/ComponentClass");
            HasError("RpcCantHaveParamComponent has invalid parameter monkeyComp",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamComponent.NetworkBehaviourClientRpcParamComponent::RpcCantHaveParamComponent(WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamComponent.NetworkBehaviourClientRpcParamComponent/ComponentClass)");
            // TODO change weaver to run checks for write/read at the same time
            //HasError("Cannot generate reader for component type ComponentClass. Use a supported type or provide a custom reader",
            //    "WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamComponent.NetworkBehaviourClientRpcParamComponent/ComponentClass");
            //HasError("RpcCantHaveParamComponent has invalid parameter monkeyComp.  Unsupported type WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamComponent.NetworkBehaviourClientRpcParamComponent/ComponentClass,  use a supported Mirror type instead",
            //    "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamComponent.NetworkBehaviourClientRpcParamComponent::RpcCantHaveParamComponent(WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamComponent.NetworkBehaviourClientRpcParamComponent/ComponentClass)");
        }

        [Test]
        public void NetworkBehaviourClientRpcParamNetworkConnection()
        {
            HasError("RpcCantHaveParamOptional has invalid parameter monkeyCon. Cannot pass NetworkConnections",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamNetworkConnection.NetworkBehaviourClientRpcParamNetworkConnection::RpcCantHaveParamOptional(Mirror.NetworkConnection)");
        }

        [Test]
        public void NetworkBehaviourClientRpcDuplicateName()
        {
            HasError("Duplicate ClientRpc name RpcCantHaveSameName",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcDuplicateName.NetworkBehaviourClientRpcDuplicateName::RpcCantHaveSameName(System.Int32,System.Int32)");
        }

        [Test]
        public void NetworkBehaviourCmdParamOut()
        {
            HasError("CmdCantHaveParamOut cannot have out parameters",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamOut.NetworkBehaviourCmdParamOut::CmdCantHaveParamOut(System.Int32&)");
        }

        [Test]
        public void NetworkBehaviourCmdParamOptional()
        {
            HasError("CmdCantHaveParamOptional cannot have optional parameters",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamOptional.NetworkBehaviourCmdParamOptional::CmdCantHaveParamOptional(System.Int32)");
        }

        [Test]
        public void NetworkBehaviourCmdParamRef()
        {
            HasError("Cannot pass Int32& by reference",
                "System.Int32&");
            HasError("CmdCantHaveParamRef has invalid parameter monkeys",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamRef.NetworkBehaviourCmdParamRef::CmdCantHaveParamRef(System.Int32&)");
            HasError("Cannot pass type Int32& by reference",
                "System.Int32&");
            HasError("CmdCantHaveParamRef has invalid parameter monkeys.  Unsupported type System.Int32&,  use a supported Mirror type instead",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamRef.NetworkBehaviourCmdParamRef::CmdCantHaveParamRef(System.Int32&)");
        }

        [Test]
        public void NetworkBehaviourCmdParamAbstract()
        {
            HasError("Cannot generate writer for abstract class AbstractClass. Use a supported type or provide a custom writer",
                "WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamAbstract.NetworkBehaviourCmdParamAbstract/AbstractClass");
            // TODO change weaver to run checks for write/read at the same time
            //HasError("AbstractClass can't be deserialized because it has no default constructor",
            //    "WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamAbstract.NetworkBehaviourCmdParamAbstract/AbstractClass");
        }

        [Test]
        public void NetworkBehaviourCmdParamComponent()
        {
            HasError("Cannot generate writer for component type ComponentClass. Use a supported type or provide a custom writer",
                "WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamComponent.NetworkBehaviourCmdParamComponent/ComponentClass");
            HasError("CmdCantHaveParamComponent has invalid parameter monkeyComp",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamComponent.NetworkBehaviourCmdParamComponent::CmdCantHaveParamComponent(WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamComponent.NetworkBehaviourCmdParamComponent/ComponentClass)");
            HasError("Cannot generate reader for component type ComponentClass. Use a supported type or provide a custom reader",
                "WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamComponent.NetworkBehaviourCmdParamComponent/ComponentClass");
            HasError("CmdCantHaveParamComponent has invalid parameter monkeyComp.  Unsupported type WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamComponent.NetworkBehaviourCmdParamComponent/ComponentClass,  use a supported Mirror type instead",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamComponent.NetworkBehaviourCmdParamComponent::CmdCantHaveParamComponent(WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamComponent.NetworkBehaviourCmdParamComponent/ComponentClass)");
        }

        [Test]
        public void NetworkBehaviourCmdParamNetworkConnection()
        {
            HasError("CmdCantHaveParamOptional has invalid parameter monkeyCon, Cannot pass NetworkConnections. Instead use 'NetworkConnectionToClient conn = null' to get the sender's connection on the server",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamNetworkConnection.NetworkBehaviourCmdParamNetworkConnection::CmdCantHaveParamOptional(Mirror.NetworkConnection)");
        }

        [Test]
        public void NetworkBehaviourCmdDuplicateName()
        {
            HasError("Duplicate Command name CmdCantHaveSameName",
                "System.Void WeaverNetworkBehaviourTests.NetworkBehaviourCmdDuplicateName.NetworkBehaviourCmdDuplicateName::CmdCantHaveSameName(System.Int32,System.Int32)");
        }
    }
}
