using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    class EmptyBehaviour : NetworkBehaviour
    {
    }

    public class NetworkBehaviourTests
    {
        GameObject gameObject;
        NetworkIdentity identity;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject();
            identity = gameObject.AddComponent<NetworkIdentity>();
        }

        [TearDown]
        public void TearDown()
        {
            GameObject.DestroyImmediate(gameObject);
        }

        [Test]
        public void IsServerOnly()
        {
            // add a behaviour
            EmptyBehaviour comp = gameObject.AddComponent<EmptyBehaviour>();

            // start server and assign netId so that isServer is true
            Transport.activeTransport = Substitute.For<Transport>();
            NetworkServer.Listen(1);
            identity.netId = 42;

            // isServerOnly should be true when isServer = true && isClient = false
            Assert.That(comp.isServer, Is.True);
            Assert.That(comp.isClient, Is.False);
            Assert.That(comp.isServerOnly, Is.True);

            // clean up
            NetworkServer.Shutdown();
            Transport.activeTransport = null;
        }
    }
}
