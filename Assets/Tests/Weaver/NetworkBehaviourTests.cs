using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class NetworkBehaviourTests : TestsBuildFromTestName
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
            IsSuccess();
        }

        [Test]
        public void NetworkBehaviourGenericInherit()
        {
            IsSuccess();
        }

        [Test]
        public void NetworkBehaviourCmdGenericArgument()
        {
            HasError("CmdCantHaveGeneric cannot have generic parameters",
                "System.Void NetworkBehaviourTests.NetworkBehaviourCmdGenericArgument.NetworkBehaviourCmdGenericArgument`1::CmdCantHaveGeneric(T)");
        }

        [Test]
        public void NetworkBehaviourCmdGenericParam()
        {
            HasError("CmdCantHaveGeneric cannot have generic parameters",
                "System.Void NetworkBehaviourTests.NetworkBehaviourCmdGenericParam.NetworkBehaviourCmdGenericParam::CmdCantHaveGeneric()");
        }

        [Test]
        public void NetworkBehaviourCmdCoroutine()
        {
            HasError("CmdCantHaveCoroutine cannot be a coroutine",
                "System.Collections.IEnumerator NetworkBehaviourTests.NetworkBehaviourCmdCoroutine.NetworkBehaviourCmdCoroutine::CmdCantHaveCoroutine()");
        }

        [Test]
        public void NetworkBehaviourCmdVoidReturn()
        {
            HasError("Use UniTask<System.Int32> to return values from [ServerRpc]",
                "System.Int32 NetworkBehaviourTests.NetworkBehaviourCmdVoidReturn.NetworkBehaviourCmdVoidReturn::CmdCantHaveNonVoidReturn()");
        }

        [Test]
        public void NetworkBehaviourClientRpcGenericArgument()
        {
            HasError("RpcCantHaveGeneric cannot have generic parameters",
                "System.Void NetworkBehaviourTests.NetworkBehaviourClientRpcGenericArgument.NetworkBehaviourClientRpcGenericArgument`1::RpcCantHaveGeneric(T)");
        }

        [Test]
        public void NetworkBehaviourClientRpcGenericParam()
        {
            HasError("RpcCantHaveGeneric cannot have generic parameters",
                "System.Void NetworkBehaviourTests.NetworkBehaviourClientRpcGenericParam.NetworkBehaviourClientRpcGenericParam::RpcCantHaveGeneric()");
        }

        [Test]
        public void NetworkBehaviourClientRpcCoroutine()
        {
            HasError("RpcCantHaveCoroutine cannot be a coroutine",
                "System.Collections.IEnumerator NetworkBehaviourTests.NetworkBehaviourClientRpcCoroutine.NetworkBehaviourClientRpcCoroutine::RpcCantHaveCoroutine()");
        }

        [Test]
        public void NetworkBehaviourClientRpcVoidReturn()
        {
            HasError("RpcCantHaveNonVoidReturn cannot return a value.  Make it void instead",
                "System.Int32 NetworkBehaviourTests.NetworkBehaviourClientRpcVoidReturn.NetworkBehaviourClientRpcVoidReturn::RpcCantHaveNonVoidReturn()");
        }

        [Test]
        public void NetworkBehaviourClientRpcParamOut()
        {
            HasError("RpcCantHaveParamOut cannot have out parameters",
                "System.Void NetworkBehaviourTests.NetworkBehaviourClientRpcParamOut.NetworkBehaviourClientRpcParamOut::RpcCantHaveParamOut(System.Int32&)");
        }

        [Test]
        public void NetworkBehaviourClientRpcParamOptional()
        {
            HasError("RpcCantHaveParamOptional cannot have optional parameters",
                "System.Void NetworkBehaviourTests.NetworkBehaviourClientRpcParamOptional.NetworkBehaviourClientRpcParamOptional::RpcCantHaveParamOptional(System.Int32)");
        }

        [Test]
        public void NetworkBehaviourClientRpcParamRef()
        {
            HasError("Cannot pass Int32& by reference",
                "System.Int32&");
            HasError("RpcCantHaveParamRef has invalid parameter monkeys",
                "System.Void NetworkBehaviourTests.NetworkBehaviourClientRpcParamRef.NetworkBehaviourClientRpcParamRef::RpcCantHaveParamRef(System.Int32&)");
            // TODO change weaver to run checks for write/read at the same time
            //HasError("Cannot pass type Int32& by reference",
            //    "System.Int32&");
            //HasError("RpcCantHaveParamRef has invalid parameter monkeys.  Unsupported type System.Int32&,  use a supported MirrorNG type instead",
            //    "System.Void NetworkBehaviourTests.NetworkBehaviourClientRpcParamRef.NetworkBehaviourClientRpcParamRef::RpcCantHaveParamRef(System.Int32&)");
        }

        [Test]
        public void NetworkBehaviourClientRpcParamAbstract()
        {
            HasError("Cannot generate writer for abstract class AbstractClass. Use a supported type or provide a custom writer",
                "NetworkBehaviourTests.NetworkBehaviourClientRpcParamAbstract.NetworkBehaviourClientRpcParamAbstract/AbstractClass");
            // TODO change weaver to run checks for write/read at the same time
            //HasError("AbstractClass can't be deserialized because it has no default constructor",
            //    "NetworkBehaviourTests.NetworkBehaviourClientRpcParamAbstract.NetworkBehaviourClientRpcParamAbstract/AbstractClass");
        }

        [Test]
        public void NetworkBehaviourClientRpcParamComponent()
        {
            HasError("Cannot generate writer for component type ComponentClass. Use a supported type or provide a custom writer",
                "NetworkBehaviourTests.NetworkBehaviourClientRpcParamComponent.NetworkBehaviourClientRpcParamComponent/ComponentClass");
            HasError("RpcCantHaveParamComponent has invalid parameter monkeyComp",
                "System.Void NetworkBehaviourTests.NetworkBehaviourClientRpcParamComponent.NetworkBehaviourClientRpcParamComponent::RpcCantHaveParamComponent(NetworkBehaviourTests.NetworkBehaviourClientRpcParamComponent.NetworkBehaviourClientRpcParamComponent/ComponentClass)");
            // TODO change weaver to run checks for write/read at the same time
            //HasError("Cannot generate reader for component type ComponentClass. Use a supported type or provide a custom reader",
            //    "NetworkBehaviourTests.NetworkBehaviourClientRpcParamComponent.NetworkBehaviourClientRpcParamComponent/ComponentClass");
            //HasError("RpcCantHaveParamComponent has invalid parameter monkeyComp.  Unsupported type NetworkBehaviourTests.NetworkBehaviourClientRpcParamComponent.NetworkBehaviourClientRpcParamComponent/ComponentClass,  use a supported MirrorNG type instead",
            //    "System.Void NetworkBehaviourTests.NetworkBehaviourClientRpcParamComponent.NetworkBehaviourClientRpcParamComponent::RpcCantHaveParamComponent(WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamComponent.NetworkBehaviourClientRpcParamComponent/ComponentClass)");
        }

        [Test]
        public void NetworkBehaviourClientRpcParamNetworkConnection()
        {
            IsSuccess();
        }

        [Test]
        public void NetworkBehaviourClientRpcParamNetworkConnectionNotFirst()
        {
            HasError("ClientRpcCantHaveParamOptional has invalid parameter monkeyCon, Cannot pass NetworkConnections","System.Void NetworkBehaviourTests.NetworkBehaviourClientRpcParamNetworkConnectionNotFirst.NetworkBehaviourClientRpcParamNetworkConnectionNotFirst::ClientRpcCantHaveParamOptional(System.Int32,Mirror.INetworkConnection)");
        }

        [Test]
        public void NetworkBehaviourClientRpcDuplicateName()
        {
            HasError("Duplicate Rpc name RpcCantHaveSameName",
                "System.Void NetworkBehaviourTests.NetworkBehaviourClientRpcDuplicateName.NetworkBehaviourClientRpcDuplicateName::RpcCantHaveSameName(System.Int32,System.Int32)");
        }

        [Test]
        public void NetworkBehaviourCmdParamOut()
        {
            HasError("CmdCantHaveParamOut cannot have out parameters",
                "System.Void NetworkBehaviourTests.NetworkBehaviourCmdParamOut.NetworkBehaviourCmdParamOut::CmdCantHaveParamOut(System.Int32&)");
        }

        [Test]
        public void NetworkBehaviourCmdParamOptional()
        {
            HasError("CmdCantHaveParamOptional cannot have optional parameters",
                "System.Void NetworkBehaviourTests.NetworkBehaviourCmdParamOptional.NetworkBehaviourCmdParamOptional::CmdCantHaveParamOptional(System.Int32)");
        }

        [Test]
        public void NetworkBehaviourCmdParamRef()
        {
            HasError("Cannot pass Int32& by reference",
                "System.Int32&");
            HasError("CmdCantHaveParamRef has invalid parameter monkeys",
                "System.Void NetworkBehaviourTests.NetworkBehaviourCmdParamRef.NetworkBehaviourCmdParamRef::CmdCantHaveParamRef(System.Int32&)");
            HasError("Cannot pass type Int32& by reference",
                "System.Int32&");
            HasError("CmdCantHaveParamRef has invalid parameter monkeys.  Unsupported type System.Int32&,  use a supported MirrorNG type instead",
                "System.Void NetworkBehaviourTests.NetworkBehaviourCmdParamRef.NetworkBehaviourCmdParamRef::CmdCantHaveParamRef(System.Int32&)");
        }

        [Test]
        public void NetworkBehaviourCmdParamAbstract()
        {
            HasError("Cannot generate writer for abstract class AbstractClass. Use a supported type or provide a custom writer",
                "NetworkBehaviourTests.NetworkBehaviourCmdParamAbstract.NetworkBehaviourCmdParamAbstract/AbstractClass");
            // TODO change weaver to run checks for write/read at the same time
            //HasError("AbstractClass can't be deserialized because it has no default constructor",
            //    "NetworkBehaviourTests.NetworkBehaviourCmdParamAbstract.NetworkBehaviourCmdParamAbstract/AbstractClass");
        }

        [Test]
        public void NetworkBehaviourCmdParamComponent()
        {
            HasError("Cannot generate writer for component type ComponentClass. Use a supported type or provide a custom writer",
                "NetworkBehaviourTests.NetworkBehaviourCmdParamComponent.NetworkBehaviourCmdParamComponent/ComponentClass");
            HasError("CmdCantHaveParamComponent has invalid parameter monkeyComp",
                "System.Void NetworkBehaviourTests.NetworkBehaviourCmdParamComponent.NetworkBehaviourCmdParamComponent::CmdCantHaveParamComponent(NetworkBehaviourTests.NetworkBehaviourCmdParamComponent.NetworkBehaviourCmdParamComponent/ComponentClass)");
            HasError("Cannot generate reader for component type ComponentClass. Use a supported type or provide a custom reader",
                "NetworkBehaviourTests.NetworkBehaviourCmdParamComponent.NetworkBehaviourCmdParamComponent/ComponentClass");
            HasError("CmdCantHaveParamComponent has invalid parameter monkeyComp.  Unsupported type NetworkBehaviourTests.NetworkBehaviourCmdParamComponent.NetworkBehaviourCmdParamComponent/ComponentClass,  use a supported MirrorNG type instead",
                "System.Void NetworkBehaviourTests.NetworkBehaviourCmdParamComponent.NetworkBehaviourCmdParamComponent::CmdCantHaveParamComponent(NetworkBehaviourTests.NetworkBehaviourCmdParamComponent.NetworkBehaviourCmdParamComponent/ComponentClass)");
        }

        [Test]
        public void NetworkBehaviourCmdParamGameObject()
        {
            IsSuccess();
        }

        [Test]
        public void NetworkBehaviourCmdDuplicateName()
        {
            HasError("Duplicate Rpc name CmdCantHaveSameName",
                "System.Void NetworkBehaviourTests.NetworkBehaviourCmdDuplicateName.NetworkBehaviourCmdDuplicateName::CmdCantHaveSameName(System.Int32,System.Int32)");
        }

        [Test]
        public void NetworkBehaviourChild()
        {
            IsSuccess();
        }
    }
}
