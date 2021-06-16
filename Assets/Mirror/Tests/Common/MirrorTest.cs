// base class for networking tests to make things easier.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    // inherited by MirrorEditModeTest / MirrorPlayModeTest
    // to call SetUp/TearDown by [SetUp]/[UnitySetUp] as needed
    public abstract class MirrorTest
    {
        // keep track of networked GameObjects so we don't have to clean them
        // up manually each time.
        // CreateNetworked() adds to the list automatically.
        public List<GameObject> instantiated;

        // we usually need the memory transport
        public MemoryTransport transport;

        public virtual void SetUp()
        {
            instantiated = new List<GameObject>();

            // need a transport to send & receive
            Transport.activeTransport = transport = new GameObject().AddComponent<MemoryTransport>();
        }

        public virtual void TearDown()
        {
            NetworkClient.Shutdown();
            NetworkServer.Shutdown();

            // some tests might modify NetworkServer.connections without ever
            // starting the server.
            // NetworkServer.Shutdown() only clears connections if it was started.
            // so let's do it manually for proper test cleanup here.
            NetworkServer.connections.Clear();

            foreach (GameObject go in instantiated)
                if (go != null)
                    GameObject.DestroyImmediate(go);

            NetworkIdentity.spawned.Clear();

            GameObject.DestroyImmediate(transport.gameObject);
            Transport.activeTransport = null;
        }

        // create a tracked GameObject for tests without Networkidentity
        protected void CreateGameObject(out GameObject go)
        {
            go = new GameObject();
            // track
            instantiated.Add(go);
        }

        // create GameObject + NetworkIdentity
        // add to tracker list if needed (useful for cleanups afterwards)
        protected void CreateNetworked(out GameObject go, out NetworkIdentity identity)
        {
            go = new GameObject();
            identity = go.AddComponent<NetworkIdentity>();
            // Awake is only called in play mode.
            // call manually for initialization.
            identity.Awake();
            // track
            instantiated.Add(go);
        }

        // create GameObject + NetworkIdentity + NetworkBehaviour<T>
        // add to tracker list if needed (useful for cleanups afterwards)
        protected void CreateNetworked<T>(out GameObject go, out NetworkIdentity identity, out T component)
            where T : NetworkBehaviour
        {
            go = new GameObject();
            identity = go.AddComponent<NetworkIdentity>();
            component = go.AddComponent<T>();
            // always set syncinterval = 0 for immediate testing
            component.syncInterval = 0;
            // Awake is only called in play mode.
            // call manually for initialization.
            identity.Awake();
            // track
            instantiated.Add(go);
        }

        // create GameObject + NetworkIdentity + 2x NetworkBehaviour<T>
        // add to tracker list if needed (useful for cleanups afterwards)
        protected void CreateNetworked<T, U>(out GameObject go, out NetworkIdentity identity, out T componentA, out U componentB)
            where T : NetworkBehaviour
            where U : NetworkBehaviour
        {
            go = new GameObject();
            identity = go.AddComponent<NetworkIdentity>();
            componentA = go.AddComponent<T>();
            componentB = go.AddComponent<U>();
            // always set syncinterval = 0 for immediate testing
            componentA.syncInterval = 0;
            componentB.syncInterval = 0;
            // Awake is only called in play mode.
            // call manually for initialization.
            identity.Awake();
            // track
            instantiated.Add(go);
        }

        // create GameObject + NetworkIdentity + 2x NetworkBehaviour<T>
        // add to tracker list if needed (useful for cleanups afterwards)
        protected void CreateNetworked<T, U, V>(out GameObject go, out NetworkIdentity identity, out T componentA, out U componentB, out V componentC)
            where T : NetworkBehaviour
            where U : NetworkBehaviour
            where V : NetworkBehaviour
        {
            go = new GameObject();
            identity = go.AddComponent<NetworkIdentity>();
            componentA = go.AddComponent<T>();
            componentB = go.AddComponent<U>();
            componentC = go.AddComponent<V>();
            // always set syncinterval = 0 for immediate testing
            componentA.syncInterval = 0;
            componentB.syncInterval = 0;
            componentC.syncInterval = 0;
            // Awake is only called in play mode.
            // call manually for initialization.
            identity.Awake();
            // track
            instantiated.Add(go);
        }

        // create GameObject + NetworkIdentity + NetworkBehaviour & SPAWN
        // => ownerConnection can be NetworkServer.localConnection if needed.
        protected void CreateNetworkedAndSpawn<T>(out GameObject go, out NetworkIdentity identity, out T component, NetworkConnection ownerConnection = null)
            where T : NetworkBehaviour
        {
            // server & client need to be active before spawning
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            CreateNetworked(out go, out identity, out component);

            // host mode object needs a connection to server for commands to work
            identity.connectionToServer = NetworkClient.connection;

            // spawn
            NetworkServer.Spawn(go, ownerConnection);
            ProcessMessages();

            // double check that we have authority if we passed an owner connection
            if (ownerConnection != null)
                Debug.Assert(component.hasAuthority == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
        }

        // fully connect client to local server
        // gives out the server's connection to client for convenience if needed
        protected void ConnectClientBlocking(out NetworkConnectionToClient connectionToClient)
        {
            NetworkClient.Connect("127.0.0.1");
            UpdateTransport();

            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));
            connectionToClient = NetworkServer.connections.Values.First();
        }

        // fully connect client to local server & authenticate
        protected void ConnectClientBlockingAuthenticated(out NetworkConnectionToClient connectionToClient)
        {
            ConnectClientBlocking(out connectionToClient);

            // authenticate server & client connections
            connectionToClient.isAuthenticated = true;
            NetworkClient.connection.isAuthenticated = true;
        }

        // fully connect client to local server & authenticate & set read
        protected void ConnectClientBlockingAuthenticatedAndReady(out NetworkConnectionToClient connectionToClient)
        {
            ConnectClientBlocking(out connectionToClient);

            // authenticate server & client connections
            connectionToClient.isAuthenticated = true;
            NetworkClient.connection.isAuthenticated = true;

            // set ready
            NetworkClient.Ready();
            ProcessMessages();
            Assert.That(connectionToClient.isReady, Is.True);
        }

        protected void UpdateTransport()
        {
            transport.ClientEarlyUpdate();
            transport.ServerEarlyUpdate();
        }

        protected void ProcessMessages()
        {
            // server & client need to be active
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            // update server & client so batched messages are flushed
            NetworkClient.NetworkLateUpdate();
            NetworkServer.NetworkLateUpdate();

            // update transport so sent messages are received
            UpdateTransport();
        }

        // helper function to create local connection pair
        protected void CreateLocalConnectionPair(out LocalConnectionToClient connectionToClient, out LocalConnectionToServer connectionToServer)
        {
            connectionToClient = new LocalConnectionToClient();
            connectionToServer = new LocalConnectionToServer();
            connectionToClient.connectionToServer = connectionToServer;
            connectionToServer.connectionToClient = connectionToClient;
        }
    }
}
