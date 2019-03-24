//#define LOG_WEAVER_OUTPUTS

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor.Compilation;

using Mirror.Weaver;

namespace Mirror
{
    [TestFixture]
    public class WeaverTest
    {
        #region Private
        List<string> m_weaverErrors = new List<string>();
        void HandleWeaverError(string msg)
        {
#if LOG_WEAVER_OUTPUTS
            Debug.LogError(msg);
#endif
            m_weaverErrors.Add(msg);
        }

        List<string> m_weaverWarnings = new List<string>();
        void HandleWeaverWarning(string msg)
        {
#if LOG_WEAVER_OUTPUTS
            Debug.LogWarning(msg);
#endif
            m_weaverWarnings.Add(msg);
        }

        private void BuildAndWeaveTestAssembly(string baseName)
        {
            WeaverAssembler.OutputFile = baseName + ".dll";
            WeaverAssembler.AddSourceFiles(new string[] { baseName + ".cs" });
            WeaverAssembler.Build();

            Assert.That(WeaverAssembler.CompilerErrors, Is.False);
            if (m_weaverErrors.Count > 0)
            {
                Assert.That(m_weaverErrors[0], Does.StartWith("Mirror.Weaver error: "));
            }
        }
        #endregion

        #region Setup and Teardown
        [OneTimeSetUp]
        public void FixtureSetup()
        {
            // TextRenderingModule is only referenced to use TextMesh type to throw errors about types from another module
            WeaverAssembler.AddReferencesByAssemblyName(new string[] { "UnityEngine.dll", "UnityEngine.CoreModule.dll", "UnityEngine.TextRenderingModule.dll", "Mirror.dll" });

            CompilationFinishedHook.UnityLogEnabled = false;
            CompilationFinishedHook.OnWeaverError += HandleWeaverError;
            CompilationFinishedHook.OnWeaverWarning += HandleWeaverWarning;
        }

        [OneTimeTearDown]
        public void FixtureCleanup()
        {
            CompilationFinishedHook.UnityLogEnabled = true;
        }

        [SetUp]
        public void TestSetup()
        {
            BuildAndWeaveTestAssembly(TestContext.CurrentContext.Test.Name);
        }

        [TearDown]
        public void TestCleanup()
        {
            WeaverAssembler.DeleteOutputOnClear = true;
            WeaverAssembler.Clear();

            m_weaverWarnings.Clear();
            m_weaverErrors.Clear();
        }
        #endregion

        #region General tests
        [Test]
        public void InvalidType()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.AtLeast(1));
            Assert.That(m_weaverErrors[0], Does.Match("please make sure to use a valid type"));
        }

        [Test]
        public void RecursionCount()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.AtLeast(1));
            Assert.That(m_weaverErrors[0], Does.Match("Check for self-referencing member variables"));
        }

        [Test]
        public void ClientGuardWrongClass()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("\\[Client\\] guard on non-NetworkBehaviour script"));
        }

        [Test]
        public void ServerGuardWrongClass()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("\\[Server\\] guard on non-NetworkBehaviour script"));
        }

        [Test]
        public void GuardCmdWrongClass()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(4));
            Assert.That(m_weaverErrors[0], Does.Match("\\[Server\\] guard on non-NetworkBehaviour script"));
            Assert.That(m_weaverErrors[1], Does.Match("\\[Server\\] guard on non-NetworkBehaviour script"));
            Assert.That(m_weaverErrors[2], Does.Match("\\[Client\\] guard on non-NetworkBehaviour script"));
            Assert.That(m_weaverErrors[3], Does.Match("\\[Client\\] guard on non-NetworkBehaviour script"));
        }

        [Test]
        public void JaggedArray()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.AtLeast(1));
            Assert.That(m_weaverErrors[0], Does.Match("Jagged and multidimensional arrays are not supported"));
        }
        #endregion

        #region SyncVar tests
        [Test]
        public void SyncVarsValid()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(0));
        }

        [Test]
        public void SyncVarsNoHook()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("SyncVar Hook function .* not found for"));
        }

        [Test]
        public void SyncVarsNoHookParams()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("SyncVar .* must have one argument"));
        }

        [Test]
        public void SyncVarsTooManyHookParams()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("SyncVar .* must have one argument"));
        }

        [Test]
        public void SyncVarsWrongHookType()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("SyncVar Hook function .* has wrong type signature for"));
        }

       [Test]
        public void SyncVarsDerivedNetworkBehaviour()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("SyncVar .* cannot be derived from NetworkBehaviour"));
        }

        [Test]
        public void SyncVarsDerivedScriptableObject()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("SyncVar .* cannot be derived from ScriptableObject"));
        }

        [Test]
        public void SyncVarsStatic()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("SyncVar .* cannot be static"));
        }

        [Test]
        public void SyncVarsGenericParam()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("SyncVar .* cannot have generic parameters"));
        }

        [Test]
        public void SyncVarsInterface()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("SyncVar .* cannot be an interface"));
        }

        [Test]
        public void SyncVarsDifferentModule()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("SyncVar .* cannot be a different module"));
        }

        [Test]
        public void SyncVarsCantBeArray()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("SyncVar .* cannot be an array"));
        }

        [Test]
        public void SyncVarsSyncList()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(0));
            Assert.That(m_weaverWarnings.Count, Is.EqualTo(2));
            Assert.That(m_weaverWarnings[0], Does.Match("SyncLists should not be marked with SyncVar"));
            Assert.That(m_weaverWarnings[1], Does.Match("SyncLists should not be marked with SyncVar"));
        }

        [Test]
        public void SyncVarsMoreThan63()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Script class .* has too many SyncVars"));
        }
        #endregion

        #region SyncList tests
        [Test]
        public void SyncListValid()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(0));
        }

        [Test]
        public void SyncListMissingParamlessCtor()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Missing parameter-less constructor"));
        }

        [Test]
        public void SyncListByteValid() {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(0));
        }
        
        #endregion

        #region SyncListStruct tests
        [Test]
        public void SyncListStructValid()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(0));
        }

        [Test]
        public void SyncListStructGenericGeneric()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Is.EqualTo("Mirror.Weaver error: GenerateSerialization for MyGenericStruct<Single> failed. Can't have generic parameters"));
        }

        [Test]
        public void SyncListStructMemberGeneric()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(2));
            Assert.That(m_weaverErrors[0], Is.EqualTo("Mirror.Weaver error: WriteReadFunc for potato [MirrorTest.MirrorTestPlayer/MyGenericStruct`1<System.Single>/MirrorTest.MirrorTestPlayer/MyGenericStruct`1<System.Single>]. Cannot have generic parameters."));
        }

        [Test]
        public void SyncListStructMemberInterface()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(2));
            Assert.That(m_weaverErrors[0], Is.EqualTo( "Mirror.Weaver error: WriteReadFunc for potato [MirrorTest.MirrorTestPlayer/IPotato/MirrorTest.MirrorTestPlayer/IPotato]. Cannot be an interface."));
        }

        [Test]
        public void SyncListStructMemberBasicType()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(3));
            Assert.That(m_weaverErrors[0], Does.Match("please make sure to use a valid type"));
            Assert.That(m_weaverErrors[1], Does.Match("Mirror.Weaver error: WriteReadFunc for nonbasicpotato type System.Object no supported"));
        }
        #endregion

        #region NetworkBehaviour tests
        [Test]
        public void NetworkBehaviourValid()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(0));
        }

        [Test]
        public void NetworkBehaviourAbstractBaseValid()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(0));
        }

        [Test]
        public void NetworkBehaviourGeneric()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("NetworkBehaviour .* cannot have generic parameters"));
        }

        [Test]
        public void NetworkBehaviourCmdGenericParam()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Command .* cannot have generic parameters"));
        }

        [Test]
        public void NetworkBehaviourCmdCoroutine()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Command .* cannot be a coroutine"));
        }

        [Test]
        public void NetworkBehaviourCmdVoidReturn()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Command .* must have a void return type"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcGenericParam()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Target Rpc .* cannot have generic parameters"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcCoroutine()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Target Rpc .* cannot be a coroutine"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcVoidReturn()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Target Rpc .* must have a void return type"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamOut()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Target Rpc function .* cannot have out parameters"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamOptional()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Target Rpcfunction .* cannot have optional parameters"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamRef()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Target Rpc function .* cannot have ref parameters"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamAbstract()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Target Rpc function .* cannot have abstract parameters"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamComponent()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Target Rpc function .* You cannot pass a Component to a remote call"));
        }

        [Test]
        public void NetworkBehaviourTargetRpcParamNetworkConnection()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(0));
        }

        [Test]
        public void NetworkBehaviourTargetRpcDuplicateName()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Duplicate Target Rpc name"));
        }

        [Test]
        public void NetworkBehaviourClientRpcGenericParam()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Rpc .* cannot have generic parameters"));
        }

        [Test]
        public void NetworkBehaviourClientRpcCoroutine()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Rpc .* cannot be a coroutine"));
        }

        [Test]
        public void NetworkBehaviourClientRpcVoidReturn()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Rpc .* must have a void return type"));
        }

        [Test]
        public void NetworkBehaviourClientRpcParamOut()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Rpc function .* cannot have out parameters"));
        }

        [Test]
        public void NetworkBehaviourClientRpcParamOptional()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Rpcfunction .* cannot have optional parameters"));
        }

        [Test]
        public void NetworkBehaviourClientRpcParamRef()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Rpc function .* cannot have ref parameters"));
        }

        [Test]
        public void NetworkBehaviourClientRpcParamAbstract()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Rpc function .* cannot have abstract parameters"));
        }

        [Test]
        public void NetworkBehaviourClientRpcParamComponent()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Rpc function .* You cannot pass a Component to a remote call"));
        }

        [Test]
        public void NetworkBehaviourClientRpcParamNetworkConnection()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(2));
            Assert.That(m_weaverErrors[0], Does.Match("Rpc .* cannot use a NetworkConnection as a parameter"));
        }

        [Test]
        public void NetworkBehaviourClientRpcDuplicateName()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Duplicate ClientRpc name"));
        }

        [Test]
        public void NetworkBehaviourCmdParamOut()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Command function .* cannot have out parameters"));
        }

        [Test]
        public void NetworkBehaviourCmdParamOptional()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Commandfunction .* cannot have optional parameters"));
        }

        [Test]
        public void NetworkBehaviourCmdParamRef()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Command function .* cannot have ref parameters"));
        }

        [Test]
        public void NetworkBehaviourCmdParamAbstract()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Command function .* cannot have abstract parameters"));
        }

        [Test]
        public void NetworkBehaviourCmdParamComponent()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Command function .* You cannot pass a Component to a remote call"));
        }

        [Test]
        public void NetworkBehaviourCmdParamNetworkConnection()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(2));
            Assert.That(m_weaverErrors[0], Does.Match("Command .* cannot use a NetworkConnection as a parameter"));
        }

        [Test]
        public void NetworkBehaviourCmdDuplicateName()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Duplicate Command name"));
        }
        #endregion

        #region Command tests
        [Test]
        public void CommandValid()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(0));
        }

        [Test]
        public void CommandStartsWithCmd()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Command function .* doesnt have 'Cmd' prefix"));
        }

        [Test]
        public void CommandCantBeStatic()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Command function .* cant be a static method"));
        }
        #endregion

        #region ClientRpc tests
        [Test]
        public void ClientRpcValid()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(0));
        }

        [Test]
        public void ClientRpcStartsWithRpc()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Rpc function .* doesnt have 'Rpc' prefix"));
        }

        [Test]
        public void ClientRpcCantBeStatic()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("ClientRpc function .* cant be a static method"));
        }
        #endregion

        #region TargetRpc tests
        [Test]
        public void TargetRpcValid()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(0));
        }

        [Test]
        public void TargetRpcStartsWithTarget()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Target Rpc function .* doesnt have 'Target' prefix"));
        }

        [Test]
        public void TargetRpcCantBeStatic()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("TargetRpc function .* cant be a static method"));
        }
        #endregion

        #region TargetRpc tests
        [Test]
        public void SyncEventValid()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(0));
        }

        [Test]
        public void SyncEventStartsWithEvent()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Event .* doesnt have 'Event' prefix"));
        }

        [Test]
        public void SyncEventParamGeneric()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Event .* cannot have generic parameters"));
        }
        #endregion

        #region MonoBehaviour tests
        [Test]
        public void MonoBehaviourValid()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(0));
        }

        [Test]
        public void MonoBehaviourSyncVar()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Script .* uses \\[SyncVar\\] .* but is not a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourSyncList()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Script .* defines field .* with type .*, but it's not a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourCommand()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Script .* uses \\[Command\\] .* but is not a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourClientRpc()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Script .* uses \\[ClientRpc\\] .* but is not a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourTargetRpc()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Script .* uses \\[TargetRpc\\] .* but is not a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourServer()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Script .* uses the attribute \\[Server\\] .* but is not a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourServerCallback()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Script .* uses the attribute \\[ServerCallback\\] .* but is not a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourClient()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Script .* uses the attribute \\[Client\\] .* but is not a NetworkBehaviour"));
        }

        [Test]
        public void MonoBehaviourClientCallback()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("Script .* uses the attribute \\[ClientCallback\\] .* but is not a NetworkBehaviour"));
        }
        #endregion

        #region Message tests
        [Test]
        public void MessageValid()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(0));
        }

        [Test]
        public void MessageSelfReferencing()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("GenerateSerialization for .* member cannot be self referencing"));
        }

        [Test]
        public void MessageInvalidSerializeFieldType()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(2));
            Assert.That(m_weaverErrors[0], Does.Match("please make sure to use a valid type"));
            Assert.That(m_weaverErrors[1], Does.Match("GenerateSerialization for .* member variables must be basic types"));
        }

        [Test]
        public void MessageInvalidDeserializeFieldType()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(3));
            Assert.That(m_weaverErrors[0], Does.Match("please make sure to use a valid type"));
            Assert.That(m_weaverErrors[1], Does.Match("GetReadFunc unable to generate function"));
            Assert.That(m_weaverErrors[2], Does.Match("GenerateDeSerialization for .* member variables must be basic types"));
        }

        [Test]
        public void MessageMemberGeneric()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("GenerateSerialization for .* member cannot have generic parameters"));
        }

        [Test]
        public void MessageMemberInterface()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(m_weaverErrors.Count, Is.EqualTo(1));
            Assert.That(m_weaverErrors[0], Does.Match("GenerateSerialization for .* member cannot be an interface"));
        }
        #endregion
    }
}
