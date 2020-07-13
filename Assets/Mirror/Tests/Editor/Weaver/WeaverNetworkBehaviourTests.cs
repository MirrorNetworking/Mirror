using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverNetworkBehaviourTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void NetworkBehaviourValid()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void NetworkBehaviourAbstractBaseValid()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void NetworkBehaviourGeneric()
        {
            Assert.That(weaverErrors, Contains.Item("NetworkBehaviourGeneric`1 cannot have generic parameters (at WeaverNetworkBehaviourTests.NetworkBehaviourGeneric.NetworkBehaviourGeneric`1)"));
        }

        [Test]
        public void NetworkBehaviourCmdGenericParam()
        {
            Assert.That(weaverErrors, Contains.Item("CmdCantHaveGeneric cannot have generic parameters (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourCmdGenericParam.NetworkBehaviourCmdGenericParam::CmdCantHaveGeneric())"));
        }

        [Test]
        public void NetworkBehaviourCmdCoroutine()
        {
            Assert.That(weaverErrors, Contains.Item("CmdCantHaveCoroutine cannot be a coroutine (at System.Collections.IEnumerator WeaverNetworkBehaviourTests.NetworkBehaviourCmdCoroutine.NetworkBehaviourCmdCoroutine::CmdCantHaveCoroutine())"));
        }

        [Test]
        public void NetworkBehaviourCmdVoidReturn()
        {
            Assert.That(weaverErrors, Contains.Item("CmdCantHaveNonVoidReturn cannot return a value.  Make it void instead (at System.Int32 WeaverNetworkBehaviourTests.NetworkBehaviourCmdVoidReturn.NetworkBehaviourCmdVoidReturn::CmdCantHaveNonVoidReturn())"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcGenericParam()
        {
            Assert.That(weaverErrors, Contains.Item("TargetRpcCantHaveGeneric cannot have generic parameters (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcGenericParam.NetworkBehaviourTargetRpcGenericParam::TargetRpcCantHaveGeneric())"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcCoroutine()
        {
            Assert.That(weaverErrors, Contains.Item("TargetRpcCantHaveCoroutine cannot be a coroutine (at System.Collections.IEnumerator WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcCoroutine.NetworkBehaviourTargetRpcCoroutine::TargetRpcCantHaveCoroutine())"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcVoidReturn()
        {
            Assert.That(weaverErrors, Contains.Item("TargetRpcCantHaveNonVoidReturn cannot return a value.  Make it void instead (at System.Int32 WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcVoidReturn.NetworkBehaviourTargetRpcVoidReturn::TargetRpcCantHaveNonVoidReturn())"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamOut()
        {
            Assert.That(weaverErrors, Contains.Item("TargetRpcCantHaveParamOut cannot have out parameters (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamOut.NetworkBehaviourTargetRpcParamOut::TargetRpcCantHaveParamOut(Mirror.INetworkConnection,System.Int32&))"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamOptional()
        {
            Assert.That(weaverErrors, Contains.Item("TargetRpcCantHaveParamOptional cannot have optional parameters (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamOptional.NetworkBehaviourTargetRpcParamOptional::TargetRpcCantHaveParamOptional(Mirror.INetworkConnection,System.Int32))"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamRef()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot pass Int32& by reference (at System.Int32&)"));
            Assert.That(weaverErrors, Contains.Item("TargetRpcCantHaveParamRef has invalid parameter monkeys (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamRef.NetworkBehaviourTargetRpcParamRef::TargetRpcCantHaveParamRef(Mirror.INetworkConnection,System.Int32&))"));
            Assert.That(weaverErrors, Contains.Item("Cannot pass type Int32& by reference (at System.Int32&)"));
            Assert.That(weaverErrors, Contains.Item("TargetRpcCantHaveParamRef has invalid parameter monkeys.  Unsupported type System.Int32&,  use a supported Mirror type instead (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamRef.NetworkBehaviourTargetRpcParamRef::TargetRpcCantHaveParamRef(Mirror.INetworkConnection,System.Int32&))"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamAbstract()
        {
            Assert.That(weaverErrors, Contains.Item("AbstractClass can't be deserialized because it has no default constructor (at WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamAbstract.NetworkBehaviourTargetRpcParamAbstract/AbstractClass)"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamComponent()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for component type ComponentClass. Use a supported type or provide a custom writer (at WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamComponent.NetworkBehaviourTargetRpcParamComponent/ComponentClass)"));
            Assert.That(weaverErrors, Contains.Item("TargetRpcCantHaveParamComponent has invalid parameter monkeyComp (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamComponent.NetworkBehaviourTargetRpcParamComponent::TargetRpcCantHaveParamComponent(Mirror.INetworkConnection,WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamComponent.NetworkBehaviourTargetRpcParamComponent/ComponentClass))"));
            Assert.That(weaverErrors, Contains.Item("Cannot generate reader for component type ComponentClass. Use a supported type or provide a custom reader (at WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamComponent.NetworkBehaviourTargetRpcParamComponent/ComponentClass)"));
            Assert.That(weaverErrors, Contains.Item("TargetRpcCantHaveParamComponent has invalid parameter monkeyComp.  Unsupported type WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamComponent.NetworkBehaviourTargetRpcParamComponent/ComponentClass,  use a supported Mirror type instead (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamComponent.NetworkBehaviourTargetRpcParamComponent::TargetRpcCantHaveParamComponent(Mirror.INetworkConnection,WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcParamComponent.NetworkBehaviourTargetRpcParamComponent/ComponentClass))"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamNetworkConnection()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void NetworkBehaviourTargetRpcDuplicateName()
        {
            Assert.That(weaverErrors, Contains.Item("Duplicate Target Rpc name TargetRpcCantHaveSameName (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourTargetRpcDuplicateName.NetworkBehaviourTargetRpcDuplicateName::TargetRpcCantHaveSameName(Mirror.INetworkConnection,System.Int32,System.Int32))"));
        }

        [Test]
        public void NetworkBehaviourClientRpcGenericParam()
        {
            Assert.That(weaverErrors, Contains.Item("RpcCantHaveGeneric cannot have generic parameters (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcGenericParam.NetworkBehaviourClientRpcGenericParam::RpcCantHaveGeneric())"));
        }

        [Test]
        public void NetworkBehaviourClientRpcCoroutine()
        {
            Assert.That(weaverErrors, Contains.Item("RpcCantHaveCoroutine cannot be a coroutine (at System.Collections.IEnumerator WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcCoroutine.NetworkBehaviourClientRpcCoroutine::RpcCantHaveCoroutine())"));
        }

        [Test]
        public void NetworkBehaviourClientRpcVoidReturn()
        {
            Assert.That(weaverErrors, Contains.Item("RpcCantHaveNonVoidReturn cannot return a value.  Make it void instead (at System.Int32 WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcVoidReturn.NetworkBehaviourClientRpcVoidReturn::RpcCantHaveNonVoidReturn())"));
        }

        [Test]
        public void NetworkBehaviourClientRpcParamOut()
        {
            Assert.That(weaverErrors, Contains.Item("RpcCantHaveParamOut cannot have out parameters (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamOut.NetworkBehaviourClientRpcParamOut::RpcCantHaveParamOut(System.Int32&))"));
        }

        [Test]
        public void NetworkBehaviourClientRpcParamOptional()
        {
            Assert.That(weaverErrors, Contains.Item("RpcCantHaveParamOptional cannot have optional parameters (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamOptional.NetworkBehaviourClientRpcParamOptional::RpcCantHaveParamOptional(System.Int32))"));
        }

        [Test]
        public void NetworkBehaviourClientRpcParamRef()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot pass Int32& by reference (at System.Int32&)"));
            Assert.That(weaverErrors, Contains.Item("RpcCantHaveParamRef has invalid parameter monkeys (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamRef.NetworkBehaviourClientRpcParamRef::RpcCantHaveParamRef(System.Int32&))"));
            Assert.That(weaverErrors, Contains.Item("Cannot pass type Int32& by reference (at System.Int32&)"));
            Assert.That(weaverErrors, Contains.Item("RpcCantHaveParamRef has invalid parameter monkeys.  Unsupported type System.Int32&,  use a supported Mirror type instead (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamRef.NetworkBehaviourClientRpcParamRef::RpcCantHaveParamRef(System.Int32&))"));
            ;
        }

        [Test]
        public void NetworkBehaviourClientRpcParamAbstract()
        {
            Assert.That(weaverErrors, Contains.Item("AbstractClass can't be deserialized because it has no default constructor (at WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamAbstract.NetworkBehaviourClientRpcParamAbstract/AbstractClass)"));
        }

        [Test]
        public void NetworkBehaviourClientRpcParamComponent()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for component type ComponentClass. Use a supported type or provide a custom writer (at WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamComponent.NetworkBehaviourClientRpcParamComponent/ComponentClass)"));
            Assert.That(weaverErrors, Contains.Item("RpcCantHaveParamComponent has invalid parameter monkeyComp (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamComponent.NetworkBehaviourClientRpcParamComponent::RpcCantHaveParamComponent(WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamComponent.NetworkBehaviourClientRpcParamComponent/ComponentClass))"));
            Assert.That(weaverErrors, Contains.Item("Cannot generate reader for component type ComponentClass. Use a supported type or provide a custom reader (at WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamComponent.NetworkBehaviourClientRpcParamComponent/ComponentClass)"));
            Assert.That(weaverErrors, Contains.Item("RpcCantHaveParamComponent has invalid parameter monkeyComp.  Unsupported type WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamComponent.NetworkBehaviourClientRpcParamComponent/ComponentClass,  use a supported Mirror type instead (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamComponent.NetworkBehaviourClientRpcParamComponent::RpcCantHaveParamComponent(WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamComponent.NetworkBehaviourClientRpcParamComponent/ComponentClass))"));
        }

        [Test]
        public void NetworkBehaviourClientRpcParamNetworkConnection()
        {
            Assert.That(weaverErrors, Contains.Item("RpcCantHaveParamOptional has invalid parameter monkeyCon, Cannot pass NetworkConnections (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcParamNetworkConnection.NetworkBehaviourClientRpcParamNetworkConnection::RpcCantHaveParamOptional(Mirror.INetworkConnection))"));
        }

        [Test]
        public void NetworkBehaviourClientRpcDuplicateName()
        {
            Assert.That(weaverErrors, Contains.Item("Duplicate ClientRpc name RpcCantHaveSameName (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourClientRpcDuplicateName.NetworkBehaviourClientRpcDuplicateName::RpcCantHaveSameName(System.Int32,System.Int32))"));
        }

        [Test]
        public void NetworkBehaviourCmdParamOut()
        {
            Assert.That(weaverErrors, Contains.Item("CmdCantHaveParamOut cannot have out parameters (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamOut.NetworkBehaviourCmdParamOut::CmdCantHaveParamOut(System.Int32&))"));
        }

        [Test]
        public void NetworkBehaviourCmdParamOptional()
        {
            Assert.That(weaverErrors, Contains.Item("CmdCantHaveParamOptional cannot have optional parameters (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamOptional.NetworkBehaviourCmdParamOptional::CmdCantHaveParamOptional(System.Int32))"));
        }

        [Test]
        public void NetworkBehaviourCmdParamRef()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot pass Int32& by reference (at System.Int32&)"));
            Assert.That(weaverErrors, Contains.Item("CmdCantHaveParamRef has invalid parameter monkeys (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamRef.NetworkBehaviourCmdParamRef::CmdCantHaveParamRef(System.Int32&))"));
            Assert.That(weaverErrors, Contains.Item("Cannot pass type Int32& by reference (at System.Int32&)"));
            Assert.That(weaverErrors, Contains.Item("CmdCantHaveParamRef has invalid parameter monkeys.  Unsupported type System.Int32&,  use a supported Mirror type instead (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamRef.NetworkBehaviourCmdParamRef::CmdCantHaveParamRef(System.Int32&))"));
        }

        [Test]
        public void NetworkBehaviourCmdParamAbstract()
        {
            Assert.That(weaverErrors, Contains.Item("AbstractClass can't be deserialized because it has no default constructor (at WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamAbstract.NetworkBehaviourCmdParamAbstract/AbstractClass)"));
        }

        [Test]
        public void NetworkBehaviourCmdParamComponent()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for component type ComponentClass. Use a supported type or provide a custom writer (at WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamComponent.NetworkBehaviourCmdParamComponent/ComponentClass)"));
            Assert.That(weaverErrors, Contains.Item("CmdCantHaveParamComponent has invalid parameter monkeyComp (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamComponent.NetworkBehaviourCmdParamComponent::CmdCantHaveParamComponent(WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamComponent.NetworkBehaviourCmdParamComponent/ComponentClass))"));
            Assert.That(weaverErrors, Contains.Item("Cannot generate reader for component type ComponentClass. Use a supported type or provide a custom reader (at WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamComponent.NetworkBehaviourCmdParamComponent/ComponentClass)"));
            Assert.That(weaverErrors, Contains.Item("CmdCantHaveParamComponent has invalid parameter monkeyComp.  Unsupported type WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamComponent.NetworkBehaviourCmdParamComponent/ComponentClass,  use a supported Mirror type instead (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamComponent.NetworkBehaviourCmdParamComponent::CmdCantHaveParamComponent(WeaverNetworkBehaviourTests.NetworkBehaviourCmdParamComponent.NetworkBehaviourCmdParamComponent/ComponentClass))"));
        }

        [Test]
        public void NetworkBehaviourCmdDuplicateName()
        {
            Assert.That(weaverErrors, Contains.Item("Duplicate ServerRpc name CmdCantHaveSameName (at System.Void WeaverNetworkBehaviourTests.NetworkBehaviourCmdDuplicateName.NetworkBehaviourCmdDuplicateName::CmdCantHaveSameName(System.Int32,System.Int32))"));
        }
    }
}
