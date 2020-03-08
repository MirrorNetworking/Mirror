using NUnit.Framework;
using UnityEngine;

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

    public class FlagsTests : ClientServerTests
    {
        #region Setup
        GameObject playerGO;
        GameObject playerGO2;

        EmptyBehaviour behavior;
        EmptyBehaviour behavior2;
        Flags flags;

        [SetUp]
        public void SetupNetworkServer()
        {
            SetupServer();

            playerGO = new GameObject();
            playerGO.AddComponent<NetworkIdentity>();
            behavior = playerGO.AddComponent<EmptyBehaviour>();
            flags = playerGO.AddComponent<Flags>();
        }

        public void SetupNetworkClient()
        {
            SetupClient();

            playerGO2 = new GameObject();
            playerGO2.AddComponent<NetworkIdentity>();
            behavior2 = playerGO2.AddComponent<EmptyBehaviour>();
            flags = playerGO2.AddComponent<Flags>();
        }

        [TearDown]
        public void ShutdownNetworkServer()
        {
            GameObject.DestroyImmediate(playerGO);

            ShutdownServer();
        }

        public void ShutdownNetworkClient()
        {
            GameObject.DestroyImmediate(playerGO2);

            ShutdownClient();
        }
        #endregion

        [Test]
        public void CanCallServerFunctionAsServer()
        {
            manager.server.Spawn(playerGO);
            Assert.That(behavior.isServer, Is.True);
            Assert.That(behavior.isClient, Is.False);

            flags.CallServerFunction();
            flags.CallServerCallbackFunction();

            Assert.That(flags.serverFunctionCalled, Is.True);
            Assert.That(flags.serverCallbackFunctionCalled, Is.True);
        }

        [Test]
        public void CannotCallClientFunctionAsServer()
        {
            manager.server.Spawn(playerGO);
            Assert.That(behavior.isServer, Is.True);
            Assert.That(behavior.isClient, Is.False);

            flags.CallClientFunction();
            flags.CallClientCallbackFunction();

            Assert.That(flags.clientFunctionCalled, Is.False);
            Assert.That(flags.clientCallbackFunctionCalled, Is.False);
        }

        // TODO: fix #68 in order to make sure these tests are working

        /*[Test]
        public void CanCallClientFunctionAsClient()
        {
            SetupNetworkClient();

            Assert.That(behavior2.isServer, Is.False);
            Assert.That(behavior2.isClient, Is.True);

            flags.CallClientFunction();
            flags.CallClientCallbackFunction();

            Assert.That(flags.clientFunctionCalled, Is.True);
            Assert.That(flags.clientCallbackFunctionCalled, Is.True);

            ShutdownNetworkClient();
        }

        [Test]
        public void CannotCallServerFunctionAsClient()
        {
            SetupNetworkClient();

            Assert.That(behavior2.isServer, Is.False);
            Assert.That(behavior2.isClient, Is.True);

            flags.CallServerFunction();
            flags.CallServerCallbackFunction();

            Assert.That(flags.serverFunctionCalled, Is.False);
            Assert.That(flags.serverCallbackFunctionCalled, Is.False);

            ShutdownNetworkClient();
        }*/
    }
}
