using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class ExampleGuards : NetworkBehaviour
    {
        public bool serverFunctionCalled;
        public bool serverCallbackFunctionCalled;
        public bool clientFunctionCalled;
        public bool clientCallbackFunctionCalled;
        public bool hasAuthorityCalled;
        public bool hasAuthorityNoErrorCalled;

        [Server]
        public void CallServerFunction()
        {
            serverFunctionCalled = true;
        }

        [Server(error=false)]
        public void CallServerCallbackFunction()
        {
            serverCallbackFunctionCalled = true;
        }

        [Client]
        public void CallClientFunction()
        {
            clientFunctionCalled = true;
        }

        [Client(error = false)]
        public void CallClientCallbackFunction()
        {
            clientCallbackFunctionCalled = true;
        }

        [HasAuthority]
        public void CallAuthorityFunction() 
        {
            hasAuthorityCalled = true;
        }

        [HasAuthority(error = false)]
        public void CallAuthorityNoErrorFunction() 
        {
            hasAuthorityNoErrorCalled = true;
        }
    }

    public class GuardsTests : ClientServerSetup<ExampleGuards>
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
            Assert.Throws<MethodInvocationException>(() =>
            {
               serverComponent.CallClientFunction();
            });
        }

        [Test]
        public void CannotCallClientCallbackFunctionAsServer()
        {
            serverComponent.CallClientCallbackFunction();
            Assert.That(serverComponent.clientCallbackFunctionCalled, Is.False);
        }

        [Test]
        public void CannotCallServerFunctionAsClient()
        {
            Assert.Throws<MethodInvocationException>(() =>
            {
               clientComponent.CallServerFunction();
            });
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

        [Test]
        public void CanCallHasAuthorityFunctionAsClient()
        {
            clientComponent.CallAuthorityFunction();
            Assert.That(clientComponent.hasAuthorityCalled, Is.True);
        }

        [Test]
        public void CanCallHasAuthorityCallbackFunctionAsClient()
        {
            clientComponent.CallAuthorityNoErrorFunction();
            Assert.That(clientComponent.hasAuthorityNoErrorCalled, Is.True);
        }

        [Test]
        public void GuardHasAuthorityError()
        {
            var obj = new GameObject("randomObject", typeof(NetworkIdentity), typeof(ExampleGuards));
            ExampleGuards guardedComponent = obj.GetComponent<ExampleGuards>();

            Assert.Throws<MethodInvocationException>( () => 
            {
                guardedComponent.CallAuthorityFunction();
            });

            Object.Destroy(obj);
        }

        [Test]
        public void GuardHasAuthorityNoError()
        {
            var obj = new GameObject("randomObject", typeof(NetworkIdentity), typeof(ExampleGuards));
            ExampleGuards guardedComponent = obj.GetComponent<ExampleGuards>();

            guardedComponent.CallAuthorityNoErrorFunction();
            Assert.That(guardedComponent.hasAuthorityNoErrorCalled, Is.False);

            Object.Destroy(obj);
        }
    }
}
