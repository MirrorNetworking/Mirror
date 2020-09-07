using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverClientServerAttributeTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void NetworkBehaviourServer()
        {
            Assert.That(weaverErrors, Is.Empty);
            CheckAddedCode(Weaver.NetworkBehaviourIsServer, "WeaverClientServerAttributeTests.NetworkBehaviourServer.NetworkBehaviourServer", "ServerOnlyMethod");

        }

        [Test]
        public void NetworkBehaviourClient()
        {
            Assert.That(weaverErrors, Is.Empty);
            CheckAddedCode(Weaver.NetworkBehaviourIsClient, "WeaverClientServerAttributeTests.NetworkBehaviourClient.NetworkBehaviourClient", "ClientOnlyMethod");
        }

        [Test]
        public void NetworkBehaviourHasAuthority()
        {
            Assert.That(weaverErrors, Is.Empty);
            CheckAddedCode(Weaver.NetworkBehaviourHasAuthority, "WeaverClientServerAttributeTests.NetworkBehaviourHasAuthority.NetworkBehaviourHasAuthority", "HasAuthorityMethod");
        }

        [Test]
        public void NetworkBehaviourLocalPlayer()
        {
            Assert.That(weaverErrors, Is.Empty);
            CheckAddedCode(Weaver.NetworkBehaviourIsLocalPlayer, "WeaverClientServerAttributeTests.NetworkBehaviourLocalPlayer.NetworkBehaviourLocalPlayer", "LocalPlayerMethod");
        }

        /// <summary>
        /// Checks that first Instructions in MethodBody is addedString
        /// </summary>
        /// <param name="addedString"></param>
        /// <param name="methodName"></param>
        static void CheckAddedCode(MethodReference methodRef, string className, string methodName)
        {
            string assemblyName = Path.Combine(WeaverAssembler.OutputDirectory, WeaverAssembler.OutputFile);
            using (var assembly = AssemblyDefinition.ReadAssembly(assemblyName))
            {
                TypeDefinition type = assembly.MainModule.GetType(className);
                MethodDefinition method = type.Methods.First(m => m.Name == methodName);
                MethodBody body = method.Body;

                Instruction top = body.Instructions[0];
                Assert.That(top.OpCode, Is.EqualTo(OpCodes.Ldarg_0));

                Instruction call = body.Instructions[1];

                Assert.That(call.OpCode, Is.EqualTo(OpCodes.Call));
                Assert.That(call.Operand.ToString(), Is.EqualTo(methodRef.ToString()));
            }
        }
    }
}
