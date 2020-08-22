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
            IsSuccess();

            string networkServerGetActive = WeaverTypes.NetworkServerGetActive.ToString();
            CheckAddedCode(networkServerGetActive, "WeaverClientServerAttributeTests.NetworkBehaviourServer.NetworkBehaviourServer", "ServerOnlyMethod");
        }

        [Test]
        public void ServerAttributeOnVirutalMethod()
        {
            IsSuccess();

            string networkServerGetActive = WeaverTypes.NetworkServerGetActive.ToString();
            CheckAddedCode(networkServerGetActive, "WeaverClientServerAttributeTests.ServerAttributeOnVirutalMethod.ServerAttributeOnVirutalMethod", "ServerOnlyMethod");
        }

        [Test]
        public void ServerAttributeOnAbstractMethod()
        {
            HasError("Server or Client Attributes can't be added to abstract method. Server and Client Attributes are not inherited so they need to be applied to the override methods instead.",
                "System.Void WeaverClientServerAttributeTests.ServerAttributeOnAbstractMethod.ServerAttributeOnAbstractMethod::ServerOnlyMethod()");
        }

        [Test]
        public void ServerAttributeOnOverrideMethod()
        {
            IsSuccess();

            string networkServerGetActive = WeaverTypes.NetworkServerGetActive.ToString();
            CheckAddedCode(networkServerGetActive, "WeaverClientServerAttributeTests.ServerAttributeOnOverrideMethod.ServerAttributeOnOverrideMethod", "ServerOnlyMethod");
        }

        [Test]
        public void NetworkBehaviourClient()
        {
            IsSuccess();

            string networkClientGetActive = WeaverTypes.NetworkClientGetActive.ToString();
            CheckAddedCode(networkClientGetActive, "WeaverClientServerAttributeTests.NetworkBehaviourClient.NetworkBehaviourClient", "ClientOnlyMethod");
        }

        [Test]
        public void ClientAttributeOnVirutalMethod()
        {
            IsSuccess();

            string networkClientGetActive = WeaverTypes.NetworkClientGetActive.ToString();
            CheckAddedCode(networkClientGetActive, "WeaverClientServerAttributeTests.ClientAttributeOnVirutalMethod.ClientAttributeOnVirutalMethod", "ClientOnlyMethod");
        }

        [Test]
        public void ClientAttributeOnAbstractMethod()
        {
            HasError("Server or Client Attributes can't be added to abstract method. Server and Client Attributes are not inherited so they need to be applied to the override methods instead.",
                "System.Void WeaverClientServerAttributeTests.ClientAttributeOnAbstractMethod.ClientAttributeOnAbstractMethod::ClientOnlyMethod()");
        }

        [Test]
        public void ClientAttributeOnOverrideMethod()
        {
            IsSuccess();

            string networkClientGetActive = WeaverTypes.NetworkClientGetActive.ToString();
            CheckAddedCode(networkClientGetActive, "WeaverClientServerAttributeTests.ClientAttributeOnOverrideMethod.ClientAttributeOnOverrideMethod", "ClientOnlyMethod");
        }

        [Test]
        public void StaticClassClient()
        {
            IsSuccess();

            string networkClientGetActive = WeaverTypes.NetworkClientGetActive.ToString();
            CheckAddedCode(networkClientGetActive, "WeaverClientServerAttributeTests.StaticClassClient.StaticClassClient", "ClientOnlyMethod");
        }
        [Test]
        public void RegularClassClient()
        {
            IsSuccess();

            string networkClientGetActive = WeaverTypes.NetworkClientGetActive.ToString();
            CheckAddedCode(networkClientGetActive, "WeaverClientServerAttributeTests.RegularClassClient.RegularClassClient", "ClientOnlyMethod");
        }
        [Test]
        public void MonoBehaviourClient()
        {
            IsSuccess();

            string networkClientGetActive = WeaverTypes.NetworkClientGetActive.ToString();
            CheckAddedCode(networkClientGetActive, "WeaverClientServerAttributeTests.MonoBehaviourClient.MonoBehaviourClient", "ClientOnlyMethod");
        }

        [Test]
        public void StaticClassServer()
        {
            IsSuccess();

            string networkServerGetActive = WeaverTypes.NetworkServerGetActive.ToString();
            CheckAddedCode(networkServerGetActive, "WeaverClientServerAttributeTests.StaticClassServer.StaticClassServer", "ServerOnlyMethod");
        }
        [Test]
        public void RegularClassServer()
        {
            IsSuccess();

            string networkServerGetActive = WeaverTypes.NetworkServerGetActive.ToString();
            CheckAddedCode(networkServerGetActive, "WeaverClientServerAttributeTests.RegularClassServer.RegularClassServer", "ServerOnlyMethod");
        }
        [Test]
        public void MonoBehaviourServer()
        {
            IsSuccess();

            string networkServerGetActive = WeaverTypes.NetworkServerGetActive.ToString();
            CheckAddedCode(networkServerGetActive, "WeaverClientServerAttributeTests.MonoBehaviourServer.MonoBehaviourServer", "ServerOnlyMethod");
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
