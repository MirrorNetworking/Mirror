using System.IO;
using System.Linq;
using Mono.CecilX;
using Mono.CecilX.Cil;
using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverClientServerAttributeTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void NetworkBehaviourServer()
        {
            Assert.That(weaverErrors, Is.Empty);

            string networkServerGetActive = WeaverTypes.NetworkServerGetActive.ToString();
            CheckAddedCode(networkServerGetActive, "WeaverClientServerAttributeTests.NetworkBehaviourServer.NetworkBehaviourServer", "ServerOnlyMethod");
        }

        [Test]
        public void ServerAttributeOnVirutalMethod()
        {
            Assert.That(weaverErrors, Is.Empty);

            string networkServerGetActive = WeaverTypes.NetworkServerGetActive.ToString();
            CheckAddedCode(networkServerGetActive, "WeaverClientServerAttributeTests.ServerAttributeOnVirutalMethod.ServerAttributeOnVirutalMethod", "ServerOnlyMethod");
        }

        [Test]
        public void ServerAttributeOnAbstractMethod()
        {
            Assert.That(weaverErrors, Contains.Item("Server or Client Attributes can't be added to abstract method. Server and Client Attributes are not inherited so they need to be applied to the override methods instead. (at System.Void WeaverClientServerAttributeTests.ServerAttributeOnAbstractMethod.ServerAttributeOnAbstractMethod::ServerOnlyMethod())"));
        }

        [Test]
        public void ServerAttributeOnOverrideMethod()
        {
            Assert.That(weaverErrors, Is.Empty);

            string networkServerGetActive = WeaverTypes.NetworkServerGetActive.ToString();
            CheckAddedCode(networkServerGetActive, "WeaverClientServerAttributeTests.ServerAttributeOnOverrideMethod.ServerAttributeOnOverrideMethod", "ServerOnlyMethod");
        }

        [Test]
        public void NetworkBehaviourClient()
        {
            Assert.That(weaverErrors, Is.Empty);

            string networkClientGetActive = WeaverTypes.NetworkClientGetActive.ToString();
            CheckAddedCode(networkClientGetActive, "WeaverClientServerAttributeTests.NetworkBehaviourClient.NetworkBehaviourClient", "ClientOnlyMethod");
        }

        [Test]
        public void ClientAttributeOnVirutalMethod()
        {
            Assert.That(weaverErrors, Is.Empty);

            string networkClientGetActive = WeaverTypes.NetworkClientGetActive.ToString();
            CheckAddedCode(networkClientGetActive, "WeaverClientServerAttributeTests.ClientAttributeOnVirutalMethod.ClientAttributeOnVirutalMethod", "ClientOnlyMethod");
        }

        [Test]
        public void ClientAttributeOnAbstractMethod()
        {
            Assert.That(weaverErrors, Contains.Item("Server or Client Attributes can't be added to abstract method. Server and Client Attributes are not inherited so they need to be applied to the override methods instead. (at System.Void WeaverClientServerAttributeTests.ClientAttributeOnAbstractMethod.ClientAttributeOnAbstractMethod::ClientOnlyMethod())"));
        }

        [Test]
        public void ClientAttributeOnOverrideMethod()
        {
            Assert.That(weaverErrors, Is.Empty);

            string networkClientGetActive = WeaverTypes.NetworkClientGetActive.ToString();
            CheckAddedCode(networkClientGetActive, "WeaverClientServerAttributeTests.ClientAttributeOnOverrideMethod.ClientAttributeOnOverrideMethod", "ClientOnlyMethod");
        }

        /// <summary>
        /// Checks that first Instructions in MethodBody is addedString
        /// </summary>
        /// <param name="addedString"></param>
        /// <param name="methodName"></param>
        static void CheckAddedCode(string addedString, string className, string methodName)
        {
            string assemblyName = Path.Combine(WeaverAssembler.OutputDirectory, WeaverAssembler.OutputFile);
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(assemblyName))
            {
                TypeDefinition type = assembly.MainModule.GetType(className);
                MethodDefinition method = type.Methods.First(m => m.Name == methodName);
                MethodBody body = method.Body;

                Instruction top = body.Instructions[0];

                Assert.AreEqual(top.OpCode, OpCodes.Call);
                Assert.AreEqual(top.Operand.ToString(), addedString);
            }
        }
    }
}
