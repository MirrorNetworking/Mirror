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
            Assert.That(weaverErrors, Contains.Item("NetworkBehaviourGeneric`1 cannot have generic parameters (at MirrorTest.NetworkBehaviourGeneric`1)" ));
        }

        [Test]
        public void NetworkBehaviourCmdGenericParam()
        {
            Assert.That(weaverErrors, Contains.Item("CmdCantHaveGeneric cannot have generic parameters (at System.Void MirrorTest.NetworkBehaviourCmdGenericParam::CmdCantHaveGeneric())" ));
        }

        [Test]
        public void NetworkBehaviourCmdCoroutine()
        {
            Assert.That(weaverErrors, Contains.Item("CmdCantHaveCoroutine cannot be a coroutine (at System.Collections.IEnumerator MirrorTest.NetworkBehaviourCmdCoroutine::CmdCantHaveCoroutine())" ));
        }

        [Test]
        public void NetworkBehaviourCmdVoidReturn()
        {
            Assert.That(weaverErrors, Contains.Item("CmdCantHaveNonVoidReturn cannot return a value.  Make it void instead (at System.Int32 MirrorTest.NetworkBehaviourCmdVoidReturn::CmdCantHaveNonVoidReturn())" ));
        }

        [Test]
        public void NetworkBehaviourTargetRpcGenericParam()
        {
            Assert.That(weaverErrors, Contains.Item("TargetRpcCantHaveGeneric cannot have generic parameters (at System.Void MirrorTest.NetworkBehaviourTargetRpcGenericParam::TargetRpcCantHaveGeneric())" ));
        }

        [Test]
        public void NetworkBehaviourTargetRpcCoroutine()
        {
            Assert.That(weaverErrors, Contains.Item("TargetRpcCantHaveCoroutine cannot be a coroutine (at System.Collections.IEnumerator MirrorTest.NetworkBehaviourTargetRpcCoroutine::TargetRpcCantHaveCoroutine())" ));
        }

        [Test]
        public void NetworkBehaviourTargetRpcVoidReturn()
        {
            Assert.That(weaverErrors, Contains.Item("TargetRpcCantHaveNonVoidReturn cannot return a value.  Make it void instead (at System.Int32 MirrorTest.NetworkBehaviourTargetRpcVoidReturn::TargetRpcCantHaveNonVoidReturn())" ));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamOut()
        {
            Assert.That(weaverErrors, Contains.Item("TargetRpcCantHaveParamOut cannot have out parameters (at System.Void MirrorTest.NetworkBehaviourTargetRpcParamOut::TargetRpcCantHaveParamOut(Mirror.INetworkConnection,System.Int32&))" ));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamOptional()
        {
            Assert.That(weaverErrors, Contains.Item("TargetRpcCantHaveParamOptional cannot have optional parameters (at System.Void MirrorTest.NetworkBehaviourTargetRpcParamOptional::TargetRpcCantHaveParamOptional(Mirror.INetworkConnection,System.Int32))" ));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamRef()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot pass Int32& by reference (at System.Int32&)"));
            Assert.That(weaverErrors, Contains.Item("TargetRpcCantHaveParamRef has invalid parameter monkeys (at System.Void MirrorTest.NetworkBehaviourTargetRpcParamRef::TargetRpcCantHaveParamRef(Mirror.INetworkConnection,System.Int32&))"));
            Assert.That(weaverErrors, Contains.Item("Cannot pass type Int32& by reference (at System.Int32&)"));
            Assert.That(weaverErrors, Contains.Item("TargetRpcCantHaveParamRef has invalid parameter monkeys.  Unsupported type System.Int32&,  use a supported Mirror type instead (at System.Void MirrorTest.NetworkBehaviourTargetRpcParamRef::TargetRpcCantHaveParamRef(Mirror.INetworkConnection,System.Int32&))" ));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamAbstract()
        {
            Assert.That(weaverErrors, Contains.Item("AbstractClass can't be deserialized because it has no default constructor (at MirrorTest.NetworkBehaviourTargetRpcParamAbstract/AbstractClass)" ));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamComponent()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for component type ComponentClass. Use a supported type or provide a custom writer (at MirrorTest.NetworkBehaviourTargetRpcParamComponent/ComponentClass)"));
            Assert.That(weaverErrors, Contains.Item("TargetRpcCantHaveParamComponent has invalid parameter monkeyComp (at System.Void MirrorTest.NetworkBehaviourTargetRpcParamComponent::TargetRpcCantHaveParamComponent(Mirror.INetworkConnection,MirrorTest.NetworkBehaviourTargetRpcParamComponent/ComponentClass))"));
            Assert.That(weaverErrors, Contains.Item("Cannot generate reader for component type ComponentClass. Use a supported type or provide a custom reader (at MirrorTest.NetworkBehaviourTargetRpcParamComponent/ComponentClass)"));
            Assert.That(weaverErrors, Contains.Item("TargetRpcCantHaveParamComponent has invalid parameter monkeyComp.  Unsupported type MirrorTest.NetworkBehaviourTargetRpcParamComponent/ComponentClass,  use a supported Mirror type instead (at System.Void MirrorTest.NetworkBehaviourTargetRpcParamComponent::TargetRpcCantHaveParamComponent(Mirror.INetworkConnection,MirrorTest.NetworkBehaviourTargetRpcParamComponent/ComponentClass))" ));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamNetworkConnection()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void NetworkBehaviourTargetRpcDuplicateName()
        {
            Assert.That(weaverErrors, Contains.Item("Duplicate Target Rpc name TargetRpcCantHaveSameName (at System.Void MirrorTest.NetworkBehaviourTargetRpcDuplicateName::TargetRpcCantHaveSameName(Mirror.INetworkConnection,System.Int32,System.Int32))" ));
        }

        [Test]
        public void NetworkBehaviourClientRpcGenericParam()
        {
            Assert.That(weaverErrors, Contains.Item("RpcCantHaveGeneric cannot have generic parameters (at System.Void MirrorTest.NetworkBehaviourClientRpcGenericParam::RpcCantHaveGeneric())"));
        }

        [Test]
        public void NetworkBehaviourClientRpcCoroutine()
        {
            Assert.That(weaverErrors, Contains.Item("RpcCantHaveCoroutine cannot be a coroutine (at System.Collections.IEnumerator MirrorTest.NetworkBehaviourClientRpcCoroutine::RpcCantHaveCoroutine())"));        }

        [Test]
        public void NetworkBehaviourClientRpcVoidReturn()
        {
            Assert.That(weaverErrors, Contains.Item("RpcCantHaveNonVoidReturn cannot return a value.  Make it void instead (at System.Int32 MirrorTest.NetworkBehaviourClientRpcVoidReturn::RpcCantHaveNonVoidReturn())" ));
        }

        [Test]
        public void NetworkBehaviourClientRpcParamOut()
        {
            Assert.That(weaverErrors, Contains.Item("RpcCantHaveParamOut cannot have out parameters (at System.Void MirrorTest.NetworkBehaviourClientRpcParamOut::RpcCantHaveParamOut(System.Int32&))"));
        }

        [Test]
        public void NetworkBehaviourClientRpcParamOptional()
        {
            Assert.That(weaverErrors, Contains.Item("RpcCantHaveParamOptional cannot have optional parameters (at System.Void MirrorTest.NetworkBehaviourClientRpcParamOptional::RpcCantHaveParamOptional(System.Int32))"));
        }

        [Test]
        public void NetworkBehaviourClientRpcParamRef()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot pass Int32& by reference (at System.Int32&)"));
            Assert.That(weaverErrors, Contains.Item("RpcCantHaveParamRef has invalid parameter monkeys (at System.Void MirrorTest.NetworkBehaviourClientRpcParamRef::RpcCantHaveParamRef(System.Int32&))"));
            Assert.That(weaverErrors, Contains.Item("Cannot pass type Int32& by reference (at System.Int32&)"));
            Assert.That(weaverErrors, Contains.Item("RpcCantHaveParamRef has invalid parameter monkeys.  Unsupported type System.Int32&,  use a supported Mirror type instead (at System.Void MirrorTest.NetworkBehaviourClientRpcParamRef::RpcCantHaveParamRef(System.Int32&))"));
            ;
        }

        [Test]
        public void NetworkBehaviourClientRpcParamAbstract()
        {
            Assert.That(weaverErrors, Contains.Item("AbstractClass can't be deserialized because it has no default constructor (at MirrorTest.NetworkBehaviourClientRpcParamAbstract/AbstractClass)"));
        }

        [Test]
        public void NetworkBehaviourClientRpcParamComponent()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for component type ComponentClass. Use a supported type or provide a custom writer (at MirrorTest.NetworkBehaviourClientRpcParamComponent/ComponentClass)"));
            Assert.That(weaverErrors, Contains.Item("RpcCantHaveParamComponent has invalid parameter monkeyComp (at System.Void MirrorTest.NetworkBehaviourClientRpcParamComponent::RpcCantHaveParamComponent(MirrorTest.NetworkBehaviourClientRpcParamComponent/ComponentClass))"));
            Assert.That(weaverErrors, Contains.Item("Cannot generate reader for component type ComponentClass. Use a supported type or provide a custom reader (at MirrorTest.NetworkBehaviourClientRpcParamComponent/ComponentClass)"));
            Assert.That(weaverErrors, Contains.Item("RpcCantHaveParamComponent has invalid parameter monkeyComp.  Unsupported type MirrorTest.NetworkBehaviourClientRpcParamComponent/ComponentClass,  use a supported Mirror type instead (at System.Void MirrorTest.NetworkBehaviourClientRpcParamComponent::RpcCantHaveParamComponent(MirrorTest.NetworkBehaviourClientRpcParamComponent/ComponentClass))"));
        }

        [Test]
        public void NetworkBehaviourClientRpcParamNetworkConnection()
        {
            Assert.That(weaverErrors, Contains.Item("RpcCantHaveParamOptional has invalid parameer monkeyCon. Cannot pass NeworkConnections (at System.Void MirrorTest.NetworkBehaviourClientRpcParamNetworkConnection::RpcCantHaveParamOptional(Mirror.INetworkConnection))"));
        }

        [Test]
        public void NetworkBehaviourClientRpcDuplicateName()
        {
            Assert.That(weaverErrors, Contains.Item("Duplicate ClientRpc name RpcCantHaveSameName (at System.Void MirrorTest.NetworkBehaviourClientRpcDuplicateName::RpcCantHaveSameName(System.Int32,System.Int32))"));
        }

        [Test]
        public void NetworkBehaviourCmdParamOut()
        {
            Assert.That(weaverErrors, Contains.Item("CmdCantHaveParamOut cannot have out parameters (at System.Void MirrorTest.NetworkBehaviourCmdParamOut::CmdCantHaveParamOut(System.Int32&))" ));
        }

        [Test]
        public void NetworkBehaviourCmdParamOptional()
        {
            Assert.That(weaverErrors, Contains.Item("CmdCantHaveParamOptional cannot have optional parameters (at System.Void MirrorTest.NetworkBehaviourCmdParamOptional::CmdCantHaveParamOptional(System.Int32))" ));
        }

        [Test]
        public void NetworkBehaviourCmdParamRef()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot pass Int32& by reference (at System.Int32&)"));
            Assert.That(weaverErrors, Contains.Item("CmdCantHaveParamRef has invalid parameter monkeys (at System.Void MirrorTest.NetworkBehaviourCmdParamRef::CmdCantHaveParamRef(System.Int32&))"));
            Assert.That(weaverErrors, Contains.Item("Cannot pass type Int32& by reference (at System.Int32&)"));
            Assert.That(weaverErrors, Contains.Item("CmdCantHaveParamRef has invalid parameter monkeys.  Unsupported type System.Int32&,  use a supported Mirror type instead (at System.Void MirrorTest.NetworkBehaviourCmdParamRef::CmdCantHaveParamRef(System.Int32&))" ));
        }

        [Test]
        public void NetworkBehaviourCmdParamAbstract()
        {
            Assert.That(weaverErrors, Contains.Item("AbstractClass can't be deserialized because it has no default constructor (at MirrorTest.NetworkBehaviourCmdParamAbstract/AbstractClass)" ));
        }

        [Test]
        public void NetworkBehaviourCmdParamComponent()
        {
            Assert.That(weaverErrors, Contains.Item("Cannot generate writer for component type ComponentClass. Use a supported type or provide a custom writer (at MirrorTest.NetworkBehaviourCmdParamComponent/ComponentClass)"));
            Assert.That(weaverErrors, Contains.Item("CmdCantHaveParamComponent has invalid parameter monkeyComp (at System.Void MirrorTest.NetworkBehaviourCmdParamComponent::CmdCantHaveParamComponent(MirrorTest.NetworkBehaviourCmdParamComponent/ComponentClass))"));
            Assert.That(weaverErrors, Contains.Item("Cannot generate reader for component type ComponentClass. Use a supported type or provide a custom reader (at MirrorTest.NetworkBehaviourCmdParamComponent/ComponentClass)"));
            Assert.That(weaverErrors, Contains.Item("CmdCantHaveParamComponent has invalid parameter monkeyComp.  Unsupported type MirrorTest.NetworkBehaviourCmdParamComponent/ComponentClass,  use a supported Mirror type instead (at System.Void MirrorTest.NetworkBehaviourCmdParamComponent::CmdCantHaveParamComponent(MirrorTest.NetworkBehaviourCmdParamComponent/ComponentClass))" ));
        }

        [Test]
        public void NetworkBehaviourCmdParamNetworkConnection()
        {
            Assert.That(weaverErrors, Contains.Item("CmdCantHaveParamOptional has invalid parameer monkeyCon. Cannot pass NeworkConnections (at System.Void MirrorTest.NetworkBehaviourCmdParamNetworkConnection::CmdCantHaveParamOptional(Mirror.INetworkConnection))" ));
        }

        [Test]
        public void NetworkBehaviourCmdDuplicateName()
        {
            Assert.That(weaverErrors, Contains.Item("Duplicate Command name CmdCantHaveSameName (at System.Void MirrorTest.NetworkBehaviourCmdDuplicateName::CmdCantHaveSameName(System.Int32,System.Int32))" ));
        }
    }
}
