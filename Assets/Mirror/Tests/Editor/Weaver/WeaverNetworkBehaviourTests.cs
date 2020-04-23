using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverNetworkBehaviourTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void NetworkBehaviourValid()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void NetworkBehaviourAbstractBaseValid()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void NetworkBehaviourGeneric()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: MirrorTest.NetworkBehaviourGeneric`1 cannot have generic parameters"));
        }

        [Test]
        public void NetworkBehaviourCmdGenericParam()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Void MirrorTest.NetworkBehaviourCmdGenericParam::CmdCantHaveGeneric() cannot have generic parameters"));
        }

        [Test]
        public void NetworkBehaviourCmdCoroutine()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Collections.IEnumerator MirrorTest.NetworkBehaviourCmdCoroutine::CmdCantHaveCoroutine() cannot be a coroutine"));
        }

        [Test]
        public void NetworkBehaviourCmdVoidReturn()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Int32 MirrorTest.NetworkBehaviourCmdVoidReturn::CmdCantHaveNonVoidReturn() cannot return a value.  Make it void instead"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcGenericParam()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Void MirrorTest.NetworkBehaviourTargetRpcGenericParam::TargetRpcCantHaveGeneric() cannot have generic parameters"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcCoroutine()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Collections.IEnumerator MirrorTest.NetworkBehaviourTargetRpcCoroutine::TargetRpcCantHaveCoroutine() cannot be a coroutine"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcVoidReturn()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Int32 MirrorTest.NetworkBehaviourTargetRpcVoidReturn::TargetRpcCantHaveNonVoidReturn() cannot return a value.  Make it void instead"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamOut()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Void MirrorTest.NetworkBehaviourTargetRpcParamOut::TargetRpcCantHaveParamOut(Mirror.NetworkConnection,System.Int32&) cannot have out parameters"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamOptional()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Void MirrorTest.NetworkBehaviourTargetRpcParamOptional::TargetRpcCantHaveParamOptional(Mirror.NetworkConnection,System.Int32) cannot have optional parameters"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamRef()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: Cannot pass System.Int32& by reference"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamAbstract()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: MirrorTest.NetworkBehaviourTargetRpcParamAbstract/AbstractClass can't be deserialized because it has no default constructor"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamComponent()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: Cannot generate writer for component type MirrorTest.NetworkBehaviourTargetRpcParamComponent/ComponentClass. Use a supported type or provide a custom writer"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamNetworkConnection()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
        }

        [Test]
        public void NetworkBehaviourTargetRpcDuplicateName()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: Duplicate Target Rpc name [MirrorTest.NetworkBehaviourTargetRpcDuplicateName:TargetRpcCantHaveSameName]"));
        }

        [Test]
        public void NetworkBehaviourClientRpcGenericParam()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Void MirrorTest.NetworkBehaviourClientRpcGenericParam::RpcCantHaveGeneric() cannot have generic parameters"));
        }

        [Test]
        public void NetworkBehaviourClientRpcCoroutine()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Collections.IEnumerator MirrorTest.NetworkBehaviourClientRpcCoroutine::RpcCantHaveCoroutine() cannot be a coroutine"));
        }

        [Test]
        public void NetworkBehaviourClientRpcVoidReturn()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Int32 MirrorTest.NetworkBehaviourClientRpcVoidReturn::RpcCantHaveNonVoidReturn() cannot return a value.  Make it void instead"));
        }

        [Test]
        public void NetworkBehaviourClientRpcParamOut()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Void MirrorTest.NetworkBehaviourClientRpcParamOut::RpcCantHaveParamOut(System.Int32&) cannot have out parameters"));
        }

        [Test]
        public void NetworkBehaviourClientRpcParamOptional()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Void MirrorTest.NetworkBehaviourClientRpcParamOptional::RpcCantHaveParamOptional(System.Int32) cannot have optional parameters"));
        }

        [Test]
        public void NetworkBehaviourClientRpcParamRef()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: Cannot pass System.Int32& by reference"));
        }

        [Test]
        public void NetworkBehaviourClientRpcParamAbstract()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: MirrorTest.NetworkBehaviourClientRpcParamAbstract/AbstractClass can't be deserialized because it has no default constructor"));
        }

        [Test]
        public void NetworkBehaviourClientRpcParamComponent()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: Cannot generate writer for component type MirrorTest.NetworkBehaviourClientRpcParamComponent/ComponentClass. Use a supported type or provide a custom writer"));
        }

        [Test]
        public void NetworkBehaviourClientRpcParamNetworkConnection()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Void MirrorTest.NetworkBehaviourClientRpcParamNetworkConnection::RpcCantHaveParamOptional(Mirror.NetworkConnection) has invalid parameer monkeyCon. Cannot pass NeworkConnections"));
        }

        [Test]
        public void NetworkBehaviourClientRpcDuplicateName()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: Duplicate ClientRpc name [MirrorTest.NetworkBehaviourClientRpcDuplicateName:RpcCantHaveSameName]"));
        }

        [Test]
        public void NetworkBehaviourCmdParamOut()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Void MirrorTest.NetworkBehaviourCmdParamOut::CmdCantHaveParamOut(System.Int32&) cannot have out parameters"));
        }

        [Test]
        public void NetworkBehaviourCmdParamOptional()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Void MirrorTest.NetworkBehaviourCmdParamOptional::CmdCantHaveParamOptional(System.Int32) cannot have optional parameters"));
        }

        [Test]
        public void NetworkBehaviourCmdParamRef()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: Cannot pass System.Int32& by reference"));
        }

        [Test]
        public void NetworkBehaviourCmdParamAbstract()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: MirrorTest.NetworkBehaviourCmdParamAbstract/AbstractClass can't be deserialized because it has no default constructor"));
        }

        [Test]
        public void NetworkBehaviourCmdParamComponent()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: Cannot generate writer for component type MirrorTest.NetworkBehaviourCmdParamComponent/ComponentClass. Use a supported type or provide a custom writer"));
        }

        [Test]
        public void NetworkBehaviourCmdParamNetworkConnection()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: System.Void MirrorTest.NetworkBehaviourCmdParamNetworkConnection::CmdCantHaveParamOptional(Mirror.NetworkConnection) has invalid parameer monkeyCon. Cannot pass NeworkConnections"));
        }

        [Test]
        public void NetworkBehaviourCmdDuplicateName()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: Duplicate Command name [MirrorTest.NetworkBehaviourCmdDuplicateName:CmdCantHaveSameName]"));
        }
    }
}
