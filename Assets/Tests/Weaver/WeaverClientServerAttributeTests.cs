using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
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
            IsSuccess();
            CheckAddedCode(
                (NetworkBehaviour nb) => nb.IsServer,
                "WeaverClientServerAttributeTests.NetworkBehaviourServer.NetworkBehaviourServer", "ServerOnlyMethod");

        }

        [Test]
        public void NetworkBehaviourClient()
        {
            IsSuccess();
            CheckAddedCode(
                (NetworkBehaviour nb) => nb.IsClient,
                "WeaverClientServerAttributeTests.NetworkBehaviourClient.NetworkBehaviourClient", "ClientOnlyMethod");
        }

        [Test]
        public void NetworkBehaviourHasAuthority()
        {
            IsSuccess();
            CheckAddedCode(
                (NetworkBehaviour nb) => nb.HasAuthority,
                "WeaverClientServerAttributeTests.NetworkBehaviourHasAuthority.NetworkBehaviourHasAuthority", "HasAuthorityMethod");
        }

        [Test]
        public void NetworkBehaviourLocalPlayer()
        {
            IsSuccess();
            CheckAddedCode(
                (NetworkBehaviour nb) => nb.IsLocalPlayer,
                "WeaverClientServerAttributeTests.NetworkBehaviourLocalPlayer.NetworkBehaviourLocalPlayer", "LocalPlayerMethod");
        }

        /// <summary>
        /// Checks that first Instructions in MethodBody is addedString
        /// </summary>
        /// <param name="addedString"></param>
        /// <param name="methodName"></param>
        void CheckAddedCode(Expression<Func<NetworkBehaviour, bool>> pred, string className, string methodName)
        {
            TypeDefinition type = assembly.MainModule.GetType(className);
            MethodDefinition method = type.Methods.First(m => m.Name == methodName);
            MethodBody body = method.Body;

            Instruction top = body.Instructions[0];
            Assert.That(top.OpCode, Is.EqualTo(OpCodes.Ldarg_0));

            var methodRef = assembly.MainModule.ImportReference(pred);

            Instruction call = body.Instructions[1];

            Assert.That(call.OpCode, Is.EqualTo(OpCodes.Call));
            Assert.That(call.Operand.ToString(), Is.EqualTo(methodRef.ToString()));
        }
    }
}
