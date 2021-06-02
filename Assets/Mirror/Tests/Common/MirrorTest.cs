// base class for networking tests to make things easier.
using System.Collections.Generic;
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
            foreach (GameObject go in instantiated)
                if (go != null)
                    GameObject.DestroyImmediate(go);

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

        protected void UpdateTransport()
        {
            transport.ClientEarlyUpdate();
            transport.ServerEarlyUpdate();
        }

        protected static void ProcessMessages()
        {
            // server & client need to be active
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            // run update so message are processed
            NetworkServer.NetworkLateUpdate();
            NetworkClient.NetworkLateUpdate();
        }
    }
}
