using System.IO;
using System.Linq;
using Mono.CecilX;
using Mono.CecilX.Cil;
using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverClientServerAttributeTests : WeaverTestsBuildFromTestName
    {
        // Debug.Log on WeaverTypes to see the strings
        const string NetworkServerGetActive = "System.Boolean Mirror.NetworkServer::get_active()";
        const string NetworkClientGetActive = "System.Boolean Mirror.NetworkClient::get_active()";

        [Test]
        public void NetworkBehaviourServer()
        {
            IsSuccess();
            CheckAddedCode(NetworkServerGetActive, "WeaverClientServerAttributeTests.NetworkBehaviourServer.NetworkBehaviourServer", "ServerOnlyMethod");
        }

        [Test]
        public void ServerAttributeOnVirutalMethod()
        {
            IsSuccess();
            CheckAddedCode(NetworkServerGetActive, "WeaverClientServerAttributeTests.ServerAttributeOnVirutalMethod.ServerAttributeOnVirutalMethod", "ServerOnlyMethod");
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
            CheckAddedCode(NetworkServerGetActive, "WeaverClientServerAttributeTests.ServerAttributeOnOverrideMethod.ServerAttributeOnOverrideMethod", "ServerOnlyMethod");
        }

        [Test]
        public void NetworkBehaviourClient()
        {
            IsSuccess();
            CheckAddedCode(NetworkClientGetActive, "WeaverClientServerAttributeTests.NetworkBehaviourClient.NetworkBehaviourClient", "ClientOnlyMethod");
        }

        [Test]
        public void ClientAttributeOnVirutalMethod()
        {
            IsSuccess();
            CheckAddedCode(NetworkClientGetActive, "WeaverClientServerAttributeTests.ClientAttributeOnVirutalMethod.ClientAttributeOnVirutalMethod", "ClientOnlyMethod");
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
            CheckAddedCode(NetworkClientGetActive, "WeaverClientServerAttributeTests.ClientAttributeOnOverrideMethod.ClientAttributeOnOverrideMethod", "ClientOnlyMethod");
        }

        [Test]
        public void StaticClassClient()
        {
            IsSuccess();
            CheckAddedCode(NetworkClientGetActive, "WeaverClientServerAttributeTests.StaticClassClient.StaticClassClient", "ClientOnlyMethod");
        }

        [Test]
        public void RegularClassClient()
        {
            IsSuccess();
            CheckAddedCode(NetworkClientGetActive, "WeaverClientServerAttributeTests.RegularClassClient.RegularClassClient", "ClientOnlyMethod");
        }

        [Test]
        public void MonoBehaviourClient()
        {
            IsSuccess();
            CheckAddedCode(NetworkClientGetActive, "WeaverClientServerAttributeTests.MonoBehaviourClient.MonoBehaviourClient", "ClientOnlyMethod");
        }

        [Test]
        public void StaticClassServer()
        {
            IsSuccess();
            CheckAddedCode(NetworkServerGetActive, "WeaverClientServerAttributeTests.StaticClassServer.StaticClassServer", "ServerOnlyMethod");
        }

        [Test]
        public void RegularClassServer()
        {
            IsSuccess();
            CheckAddedCode(NetworkServerGetActive, "WeaverClientServerAttributeTests.RegularClassServer.RegularClassServer", "ServerOnlyMethod");
        }

        [Test]
        public void MonoBehaviourServer()
        {
            IsSuccess();
            CheckAddedCode(NetworkServerGetActive, "WeaverClientServerAttributeTests.MonoBehaviourServer.MonoBehaviourServer", "ServerOnlyMethod");
        }

        // Checks that first Instructions in MethodBody is addedString
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
