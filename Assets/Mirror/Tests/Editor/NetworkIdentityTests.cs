using System.Collections;
using System.Collections.Generic;
using Mirror;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    public class NetworkIdentityTests
    {
        class MyTestComponent : NetworkBehaviour
        {
            internal bool onStartServerInvoked;

            public override void OnStartServer()
            {
                onStartServerInvoked = true;
                base.OnStartServer();
            }
        }

        // A Test behaves as an ordinary method
        [Test]
        public void OnStartServerTest()
        {
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();

            // lets add a component to check OnStartserver

            MyTestComponent component1 = gameObject.AddComponent<MyTestComponent>();
            MyTestComponent component2 = gameObject.AddComponent<MyTestComponent>();

            identity.OnStartServer();

            Assert.That(component1.onStartServerInvoked);
            Assert.That(component2.onStartServerInvoked);
        }

        class IsClientServerCheckComponent : NetworkBehaviour
        {
            // OnStartClient
            internal bool OnStartClient_isClient;
            internal bool OnStartClient_isServer;
            internal bool OnStartClient_isLocalPlayer;
            public override void OnStartClient()
            {
                OnStartClient_isClient = isClient;
                OnStartClient_isServer = isServer;
                OnStartClient_isLocalPlayer = isLocalPlayer;
            }

            // OnStartServer
            internal bool OnStartServer_isClient;
            internal bool OnStartServer_isServer;
            internal bool OnStartServer_isLocalPlayer;
            public override void OnStartServer()
            {
                OnStartServer_isClient = isClient;
                OnStartServer_isServer = isServer;
                OnStartServer_isLocalPlayer = isLocalPlayer;
            }

            // OnStartLocalPlayer
            internal bool OnStartLocalPlayer_isClient;
            internal bool OnStartLocalPlayer_isServer;
            internal bool OnStartLocalPlayer_isLocalPlayer;
            public override void OnStartLocalPlayer()
            {
                OnStartLocalPlayer_isClient = isClient;
                OnStartLocalPlayer_isServer = isServer;
                OnStartLocalPlayer_isLocalPlayer = isLocalPlayer;
            }

            // Start
            internal bool Start_isClient;
            internal bool Start_isServer;
            internal bool Start_isLocalPlayer;
            public void Start()
            {
                Start_isClient = isClient;
                Start_isServer = isServer;
                Start_isLocalPlayer = isLocalPlayer;
            }

            // OnDestroy
            internal bool OnDestroy_isClient;
            internal bool OnDestroy_isServer;
            internal bool OnDestroy_isLocalPlayer;
            public void OnDestroy()
            {
                OnDestroy_isClient = isClient;
                OnDestroy_isServer = isServer;
                OnDestroy_isLocalPlayer = isLocalPlayer;
            }
        }

        // check isClient/isServer/isLocalPlayer in server-only mode
        [Test]
        public void ServerMode_IsFlags_Test()
        {
            // start the server
            Transport.activeTransport = Substitute.For<Transport>();
            NetworkServer.Listen(1000);

            // create a networkidentity+component
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();
            IsClientServerCheckComponent component = gameObject.AddComponent<IsClientServerCheckComponent>();

            // spawn it
            NetworkServer.Spawn(gameObject);

            // OnStartServer should have been called. check the flags.
            Assert.That(component.OnStartServer_isClient, Is.EqualTo(false));
            Assert.That(component.OnStartServer_isLocalPlayer, Is.EqualTo(false));
            Assert.That(component.OnStartServer_isServer, Is.EqualTo(true));

            // stop the server
            NetworkServer.Shutdown();
            Transport.activeTransport = null;
        }

        // check isClient/isServer/isLocalPlayer in host mode
        [Test]
        public void HostMode_IsFlags_Test()
        {
            // start the server
            Transport.activeTransport = Substitute.For<Transport>();
            NetworkServer.Listen(1000);

            // start the client
            NetworkClient.ConnectHost();

            // create a networkidentity+component
            GameObject gameObject = new GameObject();
            NetworkIdentity identity = gameObject.AddComponent<NetworkIdentity>();
            IsClientServerCheckComponent component = gameObject.AddComponent<IsClientServerCheckComponent>();

            // set is as local player
            ClientScene.InternalAddPlayer(identity);

            // spawn it
            NetworkServer.Spawn(gameObject);

            // OnStartServer should have been called. check the flags.
            Assert.That(component.OnStartServer_isClient, Is.EqualTo(true));
            Assert.That(component.OnStartServer_isLocalPlayer, Is.EqualTo(true));
            Assert.That(component.OnStartServer_isServer, Is.EqualTo(true));

            // stop the client
            NetworkClient.Shutdown();
            NetworkServer.RemoveLocalConnection();
            ClientScene.Shutdown();

            // stop the server
            NetworkServer.Shutdown();
            Transport.activeTransport = null;
        }
    }
}
