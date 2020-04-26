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
            string networkServerGetActive = Weaver.NetworkBehaviourIsServer.ToString();
            CheckAddedCode(networkServerGetActive, "MirrorTest.NetworkBehaviourServer", "ServerOnlyMethod");

        }

        [Test]
        public void NetworkBehaviourClient()
        {
            Assert.That(weaverErrors, Is.Empty);
            string networkClientGetActive = Weaver.NetworkBehaviourIsClient.ToString();
            CheckAddedCode(networkClientGetActive, "MirrorTest.NetworkBehaviourClient", "ClientOnlyMethod");
        }

        /// <summary>
        /// Checks that first Instructions in MethodBody is addedString
        /// </summary>
        /// <param name="addedString"></param>
        /// <param name="methodName"></param>
        static void CheckAddedCode(string addedString, string className, string methodName)
        {
            string assemblyName = Path.Combine(WeaverAssembler.OutputDirectory,  WeaverAssembler.OutputFile);
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(assemblyName))
            {
                TypeDefinition type = assembly.MainModule.GetType(className);
                MethodDefinition method = type.Methods.First(m => m.Name == methodName);
                MethodBody body = method.Body;

                Instruction top = body.Instructions[0];
                Assert.That(top.OpCode, Is.EqualTo(OpCodes.Ldarg_0));

                Instruction call = body.Instructions[1];

                Assert.That(call.OpCode, Is.EqualTo(OpCodes.Call));
                Assert.That(call.Operand.ToString(), Is.EqualTo(addedString));
            }
        }
    }
}
