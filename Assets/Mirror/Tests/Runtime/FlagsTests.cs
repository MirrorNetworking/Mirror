using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

using static Mirror.Tests.AsyncUtil;

namespace Mirror.Tests
{
    public class Flags : NetworkBehaviour
    {
        public bool serverFunctionCalled;
        public bool serverCallbackFunctionCalled;
        public bool clientFunctionCalled;
        public bool clientCallbackFunctionCalled;

        [Server]
        public void CallServerFunction()
        {
            serverFunctionCalled = true;
        }

        [ServerCallback]
        public void CallServerCallbackFunction()
        {
            serverCallbackFunctionCalled = true;
        }

        [Client]
        public void CallClientFunction()
        {
            clientFunctionCalled = true;
        }

        [ClientCallback]
        public void CallClientCallbackFunction()
        {
            clientCallbackFunctionCalled = true;
        }
    }

    public class FlagsTests : ClientServerSetup<Flags>
    {

        [Test]
        public void CanCallServerFunctionAsServer()
        {
            serverComponent.CallServerFunction();
            Assert.That(serverComponent.serverFunctionCalled, Is.True);
        }

        [Test]
        public void CanCallServerFunctionCallbackAsServer()
        {
            serverComponent.CallServerCallbackFunction();
            Assert.That(serverComponent.serverCallbackFunctionCalled, Is.True);
        }

        [Test]
        public void CannotCallClientFunctionAsServer()
        {
            serverComponent.CallClientFunction();
            Assert.That(serverComponent.clientFunctionCalled, Is.False);
        }

        [Test]
        public void CannotCallClientCallbackFunctionAsServer()
        {
            serverComponent.CallClientFunction();
            Assert.That(serverComponent.clientCallbackFunctionCalled, Is.False);
        }

        [Test]
        public void CannotCallServerFunctionAsClient()
        {
            clientComponent.CallServerFunction();
            Assert.That(clientComponent.serverFunctionCalled, Is.False);
        }

        [Test]
        public void CannotCallServerFunctionCallbackAsClient()
        {
            clientComponent.CallServerCallbackFunction();
            Assert.That(clientComponent.serverCallbackFunctionCalled, Is.False);
        }

        [Test]
        public void CanCallClientFunctionAsClient()
        {
            clientComponent.CallClientFunction();
            Assert.That(clientComponent.clientFunctionCalled, Is.True);
        }

        [Test]
        public void CanCallClientCallbackFunctionAsClient()
        {
            clientComponent.CallClientCallbackFunction();
            Assert.That(clientComponent.clientCallbackFunctionCalled, Is.True);
        }

    }
}
