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
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
            CheckAddedCodeServer();
        }

        [Test]
        public void NetworkBehaviourClient()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
            Assert.That(weaverErrors, Is.Empty);
            CheckAddedCodeClient();
        }

        static void CheckAddedCodeServer()
        {
            string networkServerGetActive = Weaver.NetworkBehaviourIsServer.ToString();
            CheckAddedCode(networkServerGetActive, "ServerOnlyMethod");
        }

        static void CheckAddedCodeClient()
        {
            string networkClientGetActive = Weaver.NetworkBehaviourIsClient.ToString();
            CheckAddedCode(networkClientGetActive, "ClientOnlyMethod");
        }

        /// <summary>
        /// Checks that first Instructions in MethodBody is addedString
        /// </summary>
        /// <param name="addedString"></param>
        /// <param name="methodName"></param>
        static void CheckAddedCode(string addedString, string methodName)
        {
            string className = "MirrorTest.MirrorTestPlayer";

            string assemblyName = WeaverAssembler.OutputDirectory + WeaverAssembler.OutputFile;
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(assemblyName))
            {
                TypeDefinition type = assembly.MainModule.GetType(className);
                MethodDefinition method = type.Methods.First(m => m.Name == methodName);
                MethodBody body = method.Body;

                Instruction top = body.Instructions[0];
                Assert.AreEqual(top.OpCode, OpCodes.Ldarg_0);

                Instruction call = body.Instructions[1];

                Assert.AreEqual(call.OpCode, OpCodes.Call);
                Assert.AreEqual(call.Operand.ToString(), addedString);
            }
        }
    }
}
