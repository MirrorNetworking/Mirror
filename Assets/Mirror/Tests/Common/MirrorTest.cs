// base class for networking tests to make things easier.
using System;
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
        public GameObject      holder;
        public MemoryTransport transport;

        public virtual void SetUp()
        {
            instantiated = new List<GameObject>();

            // need a holder GO. with name for easier debugging.
            holder = new GameObject("MirrorTest.holder");

            // need a transport to send & receive
            Transport.active = transport = holder.AddComponent<MemoryTransport>();
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

            GameObject.DestroyImmediate(transport.gameObject);
            Transport.active = null;
            NetworkManager.singleton = null;
        }

        // create a tracked GameObject for tests without Networkidentity
        // add to tracker list if needed (useful for cleanups afterwards)
        protected void CreateGameObject(out GameObject go)
        {
            go = new GameObject();
            // track
            instantiated.Add(go);
        }

        // create GameObject + MonoBehaviour<T>
        // add to tracker list if needed (useful for cleanups afterwards)
        protected void CreateGameObject<T>(out GameObject go, out T component)
            where T : MonoBehaviour
        {
            CreateGameObject(out go);
            component = go.AddComponent<T>();
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

        protected void CreateNetworked(out GameObject go, out NetworkIdentity identity, Action<NetworkIdentity> preAwake)
        {
            go = new GameObject();
            identity = go.AddComponent<NetworkIdentity>();
            preAwake(identity);
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

        // create GameObject + NetworkIdentity + NetworkBehaviour<T> in children
        // add to tracker list if needed (useful for cleanups afterwards)
        protected void CreateNetworkedChild<T>(out GameObject parent, out GameObject child, out NetworkIdentity identity, out T component)
            where T : NetworkBehaviour
        {
            // create a GameObject with child
            parent = new GameObject("parent");
            child = new GameObject("child");
            child.transform.SetParent(parent.transform);

            // setup NetworkIdentity + NetworkBehaviour in child
            identity = parent.AddComponent<NetworkIdentity>();
            component = child.AddComponent<T>();
            // always set syncinterval = 0 for immediate testing
            component.syncInterval = 0;
            // Awake is only called in play mode.
            // call manually for initialization.
            identity.Awake();
            // track
            instantiated.Add(parent);
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

        // create GameObject + NetworkIdentity + NetworkBehaviour<T> in children
        // add to tracker list if needed (useful for cleanups afterwards)
        protected void CreateNetworkedChild<T, U>(out GameObject parent, out GameObject child, out NetworkIdentity identity, out T componentA, out U componentB)
            where T : NetworkBehaviour
            where U : NetworkBehaviour
        {
            // create a GameObject with child
            parent = new GameObject("parent");
            child = new GameObject("child");
            child.transform.SetParent(parent.transform);

            // setup NetworkIdentity + NetworkBehaviour in child
            identity = parent.AddComponent<NetworkIdentity>();
            componentA = child.AddComponent<T>();
            componentB = child.AddComponent<U>();
            // always set syncinterval = 0 for immediate testing
            componentA.syncInterval = 0;
            componentB.syncInterval = 0;
            // Awake is only called in play mode.
            // call manually for initialization.
            identity.Awake();
            // track
            instantiated.Add(parent);
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

        // create GameObject + NetworkIdentity + NetworkBehaviour<T> in children
        // add to tracker list if needed (useful for cleanups afterwards)
        protected void CreateNetworkedChild<T, U, V>(out GameObject parent, out GameObject child, out NetworkIdentity identity, out T componentA, out U componentB, out V componentC)
            where T : NetworkBehaviour
            where U : NetworkBehaviour
            where V : NetworkBehaviour
        {
            // create a GameObject with child
            parent = new GameObject("parent");
            child = new GameObject("child");
            child.transform.SetParent(parent.transform);

            // setup NetworkIdentity + NetworkBehaviour in child
            identity = parent.AddComponent<NetworkIdentity>();
            componentA = child.AddComponent<T>();
            componentB = child.AddComponent<U>();
            componentC = child.AddComponent<V>();
            // always set syncinterval = 0 for immediate testing
            componentA.syncInterval = 0;
            componentB.syncInterval = 0;
            componentC.syncInterval = 0;
            // Awake is only called in play mode.
            // call manually for initialization.
            identity.Awake();
            // track
            instantiated.Add(parent);
        }

        // create GameObject + NetworkIdentity + NetworkBehaviour & SPAWN
        // => ownerConnection can be NetworkServer.localConnection if needed.
        protected void CreateNetworkedAndSpawn(out GameObject go, out NetworkIdentity identity, NetworkConnectionToClient ownerConnection = null)
        {
            // server & client need to be active before spawning
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            CreateNetworked(out go, out identity);

            // spawn
            NetworkServer.Spawn(go, ownerConnection);
            ProcessMessages();
        }

        // create GameObject + NetworkIdentity + NetworkBehaviour & SPAWN
        // => ownerConnection can be NetworkServer.localConnection if needed.
        // => returns objects from client and from server.
        //    will be same in host mode.
        protected void CreateNetworkedAndSpawn(
            out GameObject serverGO, out NetworkIdentity serverIdentity,
            out GameObject clientGO, out NetworkIdentity clientIdentity,
            NetworkConnectionToClient ownerConnection = null)
        {
            // server & client need to be active before spawning
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            // create one on server, one on client
            // (spawning has to find it on client, it doesn't create it)
            CreateNetworked(out serverGO, out serverIdentity);
            CreateNetworked(out clientGO, out clientIdentity);

            // give both a scene id and register it on client for spawnables
            clientIdentity.sceneId = serverIdentity.sceneId = (ulong)serverGO.GetHashCode();
            NetworkClient.spawnableObjects[clientIdentity.sceneId] = clientIdentity;

            // spawn
            NetworkServer.Spawn(serverGO, ownerConnection);
            ProcessMessages();

            // double check isServer/isClient. avoids debugging headaches.
            Assert.That(serverIdentity.isServer, Is.True);
            Assert.That(clientIdentity.isClient, Is.True);

            // double check that we have authority if we passed an owner connection
            if (ownerConnection != null)
                Debug.Assert(clientIdentity.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");

            // make sure the client really spawned it.
            Assert.That(NetworkClient.spawned.ContainsKey(serverIdentity.netId));
        }

        // create GameObject + NetworkIdentity + NetworkBehaviour & SPAWN
        // => preAwake callbacks can be used to add network behaviours to the NI
        // => ownerConnection can be NetworkServer.localConnection if needed.
        // => returns objects from client and from server.
        //    will be same in host mode.
        protected void CreateNetworkedAndSpawn(
            out GameObject serverGO, out NetworkIdentity serverIdentity, Action<NetworkIdentity> serverPreAwake,
            out GameObject clientGO, out NetworkIdentity clientIdentity, Action<NetworkIdentity> clientPreAwake,
            NetworkConnectionToClient ownerConnection = null)
        {
            // server & client need to be active before spawning
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            // create one on server, one on client
            // (spawning has to find it on client, it doesn't create it)
            CreateNetworked(out serverGO, out serverIdentity, serverPreAwake);
            CreateNetworked(out clientGO, out clientIdentity, clientPreAwake);

            // give both a scene id and register it on client for spawnables
            clientIdentity.sceneId = serverIdentity.sceneId = (ulong)serverGO.GetHashCode();
            NetworkClient.spawnableObjects[clientIdentity.sceneId] = clientIdentity;

            // spawn
            NetworkServer.Spawn(serverGO, ownerConnection);
            ProcessMessages();

            // double check isServer/isClient. avoids debugging headaches.
            Assert.That(serverIdentity.isServer, Is.True);
            Assert.That(clientIdentity.isClient, Is.True);

            // double check that we have authority if we passed an owner connection
            if (ownerConnection != null)
                Debug.Assert(clientIdentity.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");

            // make sure the client really spawned it.
            Assert.That(NetworkClient.spawned.ContainsKey(serverIdentity.netId));
        }
        // create GameObject + NetworkIdentity + NetworkBehaviour & SPAWN
        // => ownerConnection can be NetworkServer.localConnection if needed.
        protected void CreateNetworkedAndSpawn<T>(out GameObject go, out NetworkIdentity identity, out T component, NetworkConnectionToClient ownerConnection = null)
            where T : NetworkBehaviour
        {
            // server & client need to be active before spawning
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            CreateNetworked(out go, out identity, out component);

            // spawn
            NetworkServer.Spawn(go, ownerConnection);
            ProcessMessages();

            // double check that we have authority if we passed an owner connection
            if (ownerConnection != null)
                Debug.Assert(component.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
        }

        // create GameObject + NetworkIdentity + NetworkBehaviour in children & SPAWN
        // => ownerConnection can be NetworkServer.localConnection if needed.
        protected void CreateNetworkedChildAndSpawn<T>(out GameObject parent, out GameObject child, out NetworkIdentity identity, out T component, NetworkConnectionToClient ownerConnection = null)
            where T : NetworkBehaviour
        {
            // server & client need to be active before spawning
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            CreateNetworkedChild(out parent, out child, out identity, out component);

            // spawn
            NetworkServer.Spawn(parent, ownerConnection);
            ProcessMessages();

            // double check that we have authority if we passed an owner connection
            if (ownerConnection != null)
                Debug.Assert(component.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
        }

        // create GameObject + NetworkIdentity + NetworkBehaviour & SPAWN
        // => ownerConnection can be NetworkServer.localConnection if needed.
        // => returns objects from client and from server.
        //    will be same in host mode.
        protected void CreateNetworkedAndSpawn<T>(
            out GameObject serverGO, out NetworkIdentity serverIdentity, out T serverComponent,
            out GameObject clientGO, out NetworkIdentity clientIdentity, out T clientComponent,
            NetworkConnectionToClient ownerConnection = null)
            where T : NetworkBehaviour
        {
            // server & client need to be active before spawning
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            // create one on server, one on client
            // (spawning has to find it on client, it doesn't create it)
            CreateNetworked(out serverGO, out serverIdentity, out serverComponent);
            CreateNetworked(out clientGO, out clientIdentity, out clientComponent);

            // give both a scene id and register it on client for spawnables
            clientIdentity.sceneId = serverIdentity.sceneId = (ulong)serverGO.GetHashCode();
            NetworkClient.spawnableObjects[clientIdentity.sceneId] = clientIdentity;

            // spawn
            NetworkServer.Spawn(serverGO, ownerConnection);
            ProcessMessages();

            // double check isServer/isClient. avoids debugging headaches.
            Assert.That(serverIdentity.isServer, Is.True);
            Assert.That(clientIdentity.isClient, Is.True);

            // double check that we have authority if we passed an owner connection
            if (ownerConnection != null)
                Debug.Assert(clientComponent.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");

            // make sure the client really spawned it.
            Assert.That(NetworkClient.spawned.ContainsKey(serverIdentity.netId));
        }

        // create GameObject + NetworkIdentity + NetworkBehaviour in children & SPAWN
        // => ownerConnection can be NetworkServer.localConnection if needed.
        // => returns objects from client and from server.
        //    will be same in host mode.
        protected void CreateNetworkedChildAndSpawn<T>(
            out GameObject serverParent, out GameObject serverChild, out NetworkIdentity serverIdentity, out T serverComponent,
            out GameObject clientParent, out GameObject clientChild, out NetworkIdentity clientIdentity, out T clientComponent,
            NetworkConnectionToClient ownerConnection = null)
            where T : NetworkBehaviour
        {
            // server & client need to be active before spawning
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            // create one on server, one on client
            // (spawning has to find it on client, it doesn't create it)
            CreateNetworkedChild(out serverParent, out serverChild, out serverIdentity, out serverComponent);
            CreateNetworkedChild(out clientParent, out clientChild, out clientIdentity, out clientComponent);

            // give both a scene id and register it on client for spawnables
            clientIdentity.sceneId = serverIdentity.sceneId = (ulong)serverParent.GetHashCode();
            NetworkClient.spawnableObjects[clientIdentity.sceneId] = clientIdentity;

            // spawn
            NetworkServer.Spawn(serverParent, ownerConnection);
            ProcessMessages();

            // double check isServer/isClient. avoids debugging headaches.
            Assert.That(serverIdentity.isServer, Is.True);
            Assert.That(clientIdentity.isClient, Is.True);

            // double check that we have authority if we passed an owner connection
            if (ownerConnection != null)
                Debug.Assert(clientComponent.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");

            // make sure the client really spawned it.
            Assert.That(NetworkClient.spawned.ContainsKey(serverIdentity.netId));
        }

        // create GameObject + NetworkIdentity + NetworkBehaviour & SPAWN
        // => ownerConnection can be NetworkServer.localConnection if needed.
        protected void CreateNetworkedAndSpawn<T, U>(out GameObject go, out NetworkIdentity identity, out T componentA, out U componentB, NetworkConnectionToClient ownerConnection = null)
            where T : NetworkBehaviour
            where U : NetworkBehaviour
        {
            // server & client need to be active before spawning
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            CreateNetworked(out go, out identity, out componentA, out componentB);

            // spawn
            NetworkServer.Spawn(go, ownerConnection);
            ProcessMessages();

            // double check that we have authority if we passed an owner connection
            if (ownerConnection != null)
            {
                Debug.Assert(componentA.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
                Debug.Assert(componentB.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
            }
        }

        // create GameObject + NetworkIdentity + NetworkBehaviour & SPAWN
        // => ownerConnection can be NetworkServer.localConnection if needed.
        protected void CreateNetworkedChildAndSpawn<T, U>(out GameObject parent, out GameObject child, out NetworkIdentity identity, out T componentA, out U componentB, NetworkConnectionToClient ownerConnection = null)
            where T : NetworkBehaviour
            where U : NetworkBehaviour
        {
            // server & client need to be active before spawning
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            CreateNetworkedChild(out parent, out child, out identity, out componentA, out componentB);

            // spawn
            NetworkServer.Spawn(parent, ownerConnection);
            ProcessMessages();

            // double check that we have authority if we passed an owner connection
            if (ownerConnection != null)
            {
                Debug.Assert(componentA.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
                Debug.Assert(componentB.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
            }
        }

        // create GameObject + NetworkIdentity + NetworkBehaviour & SPAWN
        // => ownerConnection can be NetworkServer.localConnection if needed.
        // => returns objects from client and from server.
        //    will be same in host mode.
        protected void CreateNetworkedAndSpawn<T, U>(
            out GameObject serverGO, out NetworkIdentity serverIdentity, out T serverComponentA, out U serverComponentB,
            out GameObject clientGO, out NetworkIdentity clientIdentity, out T clientComponentA, out U clientComponentB,
            NetworkConnectionToClient ownerConnection = null)
            where T : NetworkBehaviour
            where U : NetworkBehaviour
        {
            // server & client need to be active before spawning
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            // create one on server, one on client
            // (spawning has to find it on client, it doesn't create it)
            CreateNetworked(out serverGO, out serverIdentity, out serverComponentA, out serverComponentB);
            CreateNetworked(out clientGO, out clientIdentity, out clientComponentA, out clientComponentB);

            // give both a scene id and register it on client for spawnables
            clientIdentity.sceneId = serverIdentity.sceneId = (ulong)serverGO.GetHashCode();
            NetworkClient.spawnableObjects[clientIdentity.sceneId] = clientIdentity;

            // spawn
            NetworkServer.Spawn(serverGO, ownerConnection);
            ProcessMessages();

            // double check isServer/isClient. avoids debugging headaches.
            Assert.That(serverIdentity.isServer, Is.True);
            Assert.That(clientIdentity.isClient, Is.True);

            // double check that we have authority if we passed an owner connection
            if (ownerConnection != null)
            {
                Debug.Assert(clientComponentA.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
                Debug.Assert(clientComponentB.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
            }

            // make sure the client really spawned it.
            Assert.That(NetworkClient.spawned.ContainsKey(serverIdentity.netId));
        }

        // create GameObject + NetworkIdentity + NetworkBehaviour in children & SPAWN
        // => ownerConnection can be NetworkServer.localConnection if needed.
        // => returns objects from client and from server.
        //    will be same in host mode.
        protected void CreateNetworkedChildAndSpawn<T, U>(
            out GameObject serverParent, out GameObject serverChild, out NetworkIdentity serverIdentity, out T serverComponentA, out U serverComponentB,
            out GameObject clientParent, out GameObject clientChild, out NetworkIdentity clientIdentity, out T clientComponentA, out U clientComponentB,
            NetworkConnectionToClient ownerConnection = null)
            where T : NetworkBehaviour
            where U : NetworkBehaviour
        {
            // server & client need to be active before spawning
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            // create one on server, one on client
            // (spawning has to find it on client, it doesn't create it)
            CreateNetworkedChild(out serverParent, out serverChild, out serverIdentity, out serverComponentA, out serverComponentB);
            CreateNetworkedChild(out clientParent, out clientChild, out clientIdentity, out clientComponentA, out clientComponentB);

            // give both a scene id and register it on client for spawnables
            clientIdentity.sceneId = serverIdentity.sceneId = (ulong)serverParent.GetHashCode();
            NetworkClient.spawnableObjects[clientIdentity.sceneId] = clientIdentity;

            // spawn
            NetworkServer.Spawn(serverParent, ownerConnection);
            ProcessMessages();

            // double check isServer/isClient. avoids debugging headaches.
            Assert.That(serverIdentity.isServer, Is.True);
            Assert.That(clientIdentity.isClient, Is.True);

            // double check that we have authority if we passed an owner connection
            if (ownerConnection != null)
            {
                Debug.Assert(clientComponentA.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
                Debug.Assert(clientComponentB.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
            }

            // make sure the client really spawned it.
            Assert.That(NetworkClient.spawned.ContainsKey(serverIdentity.netId));
        }

        // create GameObject + NetworkIdentity + NetworkBehaviour & SPAWN
        // => ownerConnection can be NetworkServer.localConnection if needed.
        protected void CreateNetworkedAndSpawn<T, U, V>(out GameObject go, out NetworkIdentity identity, out T componentA, out U componentB, out V componentC, NetworkConnectionToClient ownerConnection = null)
            where T : NetworkBehaviour
            where U : NetworkBehaviour
            where V : NetworkBehaviour
        {
            // server & client need to be active before spawning
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            CreateNetworked(out go, out identity, out componentA, out componentB, out componentC);

            // spawn
            NetworkServer.Spawn(go, ownerConnection);
            ProcessMessages();

            // double check that we have authority if we passed an owner connection
            if (ownerConnection != null)
            {
                Debug.Assert(componentA.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
                Debug.Assert(componentB.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
                Debug.Assert(componentC.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
            }
        }

        // create GameObject + NetworkIdentity + NetworkBehaviour & SPAWN
        // => ownerConnection can be NetworkServer.localConnection if needed.
        protected void CreateNetworkedChildAndSpawn<T, U, V>(out GameObject parent, out GameObject child, out NetworkIdentity identity, out T componentA, out U componentB, out V componentC, NetworkConnectionToClient ownerConnection = null)
            where T : NetworkBehaviour
            where U : NetworkBehaviour
            where V : NetworkBehaviour
        {
            // server & client need to be active before spawning
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            CreateNetworkedChild(out parent, out child, out identity, out componentA, out componentB, out componentC);

            // spawn
            NetworkServer.Spawn(parent, ownerConnection);
            ProcessMessages();

            // double check that we have authority if we passed an owner connection
            if (ownerConnection != null)
            {
                Debug.Assert(componentA.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
                Debug.Assert(componentB.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
                Debug.Assert(componentC.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
            }
        }

        // create GameObject + NetworkIdentity + NetworkBehaviour in children & SPAWN
        // => ownerConnection can be NetworkServer.localConnection if needed.
        // => returns objects from client and from server.
        //    will be same in host mode.
        protected void CreateNetworkedAndSpawn<T, U, V>(
            out GameObject serverGO, out NetworkIdentity serverIdentity, out T serverComponentA, out U serverComponentB, out V serverComponentC,
            out GameObject clientGO, out NetworkIdentity clientIdentity, out T clientComponentA, out U clientComponentB, out V clientComponentC,
            NetworkConnectionToClient ownerConnection = null)
            where T : NetworkBehaviour
            where U : NetworkBehaviour
            where V : NetworkBehaviour
        {
            // server & client need to be active before spawning
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            // create one on server, one on client
            // (spawning has to find it on client, it doesn't create it)
            CreateNetworked(out serverGO, out serverIdentity, out serverComponentA, out serverComponentB, out serverComponentC);
            CreateNetworked(out clientGO, out clientIdentity, out clientComponentA, out clientComponentB, out clientComponentC);

            // give both a scene id and register it on client for spawnables
            clientIdentity.sceneId = serverIdentity.sceneId = (ulong)serverGO.GetHashCode();
            NetworkClient.spawnableObjects[clientIdentity.sceneId] = clientIdentity;

            // spawn
            NetworkServer.Spawn(serverGO, ownerConnection);
            ProcessMessages();

            // double check isServer/isClient. avoids debugging headaches.
            Assert.That(serverIdentity.isServer, Is.True);
            Assert.That(clientIdentity.isClient, Is.True);

            // double check that we have authority if we passed an owner connection
            if (ownerConnection != null)
            {
                Debug.Assert(clientComponentA.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
                Debug.Assert(clientComponentB.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
                Debug.Assert(clientComponentC.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
            }

            // make sure the client really spawned it.
            Assert.That(NetworkClient.spawned.ContainsKey(serverIdentity.netId));
        }

        // create GameObject + NetworkIdentity + NetworkBehaviour in children & SPAWN
        // => ownerConnection can be NetworkServer.localConnection if needed.
        // => returns objects from client and from server.
        //    will be same in host mode.
        protected void CreateNetworkedChildAndSpawn<T, U, V>(
            out GameObject serverParent, out GameObject serverChild, out NetworkIdentity serverIdentity, out T serverComponentA, out U serverComponentB, out V serverComponentC,
            out GameObject clientParent, out GameObject clientChild, out NetworkIdentity clientIdentity, out T clientComponentA, out U clientComponentB, out V clientComponentC,
            NetworkConnectionToClient ownerConnection = null)
            where T : NetworkBehaviour
            where U : NetworkBehaviour
            where V : NetworkBehaviour
        {
            // server & client need to be active before spawning
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            // create one on server, one on client
            // (spawning has to find it on client, it doesn't create it)
            CreateNetworkedChild(out serverParent, out serverChild, out serverIdentity, out serverComponentA, out serverComponentB, out serverComponentC);
            CreateNetworkedChild(out clientParent, out clientChild, out clientIdentity, out clientComponentA, out clientComponentB, out clientComponentC);

            // give both a scene id and register it on client for spawnables
            clientIdentity.sceneId = serverIdentity.sceneId = (ulong)serverParent.GetHashCode();
            NetworkClient.spawnableObjects[clientIdentity.sceneId] = clientIdentity;

            // spawn
            NetworkServer.Spawn(serverParent, ownerConnection);
            ProcessMessages();

            // double check isServer/isClient. avoids debugging headaches.
            Assert.That(serverIdentity.isServer, Is.True);
            Assert.That(clientIdentity.isClient, Is.True);

            // double check that we have authority if we passed an owner connection
            if (ownerConnection != null)
            {
                Debug.Assert(clientComponentA.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
                Debug.Assert(clientComponentB.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
                Debug.Assert(clientComponentC.isOwned == true, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");
            }

            // make sure the client really spawned it.
            Assert.That(NetworkClient.spawned.ContainsKey(serverIdentity.netId));
        }

        // create GameObject + NetworkIdentity + NetworkBehaviour & SPAWN PLAYER.
        // often times, we really need a player object for the client to receive
        // certain messages.
        protected void CreateNetworkedAndSpawnPlayer(out GameObject go, out NetworkIdentity identity, NetworkConnectionToClient ownerConnection)
        {
            // server & client need to be active before spawning
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            // create a networked object
            CreateNetworked(out go, out identity);

            // add as player & process spawn message on client.
            NetworkServer.AddPlayerForConnection(ownerConnection, go);
            ProcessMessages();
        }

        // create GameObject + NetworkIdentity + NetworkBehaviour & SPAWN PLAYER.
        // often times, we really need a player object for the client to receive
        // certain messages.
        // => returns objects from client and from server.
        //    will be same in host mode.
        protected void CreateNetworkedAndSpawnPlayer(
            out GameObject serverGO, out NetworkIdentity serverIdentity,
            out GameObject clientGO, out NetworkIdentity clientIdentity,
            NetworkConnectionToClient ownerConnection)
        {
            // server & client need to be active before spawning
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            // create one on server, one on client
            // (spawning has to find it on client, it doesn't create it)
            CreateNetworked(out serverGO, out serverIdentity);
            CreateNetworked(out clientGO, out clientIdentity);

            // give both a scene id and register it on client for spawnables
            clientIdentity.sceneId = serverIdentity.sceneId = (ulong)serverGO.GetHashCode();
            NetworkClient.spawnableObjects[clientIdentity.sceneId] = clientIdentity;

            // IMPORTANT: OnSpawn finds 'sceneId' in .spawnableObjects.
            // only those who are ConsiderForSpawn() are in there.
            // for scene objects to be considered, they need to be disabled.
            // (it'll be active by the time we return here)
            clientGO.SetActive(false);

            // add as player & process spawn message on client.
            NetworkServer.AddPlayerForConnection(ownerConnection, serverGO);
            ProcessMessages();

            // double check isServer/isClient. avoids debugging headaches.
            Assert.That(serverIdentity.isServer, Is.True);
            Assert.That(clientIdentity.isClient, Is.True);

            // make sure the client really spawned it.
            Assert.That(clientGO.activeSelf, Is.True);
            Assert.That(NetworkClient.spawned.ContainsKey(serverIdentity.netId));

            // double check that client object's isClient is really true!
            // previously a test magically failed because isClient was false
            // even though it should've been true!
            Assert.That(clientIdentity.isClient, Is.True);
        }

        // create GameObject + NetworkIdentity + NetworkBehaviour & SPAWN PLAYER.
        // often times, we really need a player object for the client to receive
        // certain messages.
        protected void CreateNetworkedAndSpawnPlayer<T>(out GameObject go, out NetworkIdentity identity, out T component, NetworkConnectionToClient ownerConnection)
            where T : NetworkBehaviour
        {
            // server & client need to be active before spawning
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            // create a networked object
            CreateNetworked(out go, out identity, out component);

            // add as player & process spawn message on client.
            NetworkServer.AddPlayerForConnection(ownerConnection, go);
            ProcessMessages();
        }

        // create GameObject + NetworkIdentity + NetworkBehaviour & SPAWN PLAYER.
        // often times, we really need a player object for the client to receive
        // certain messages.
        // => returns objects from client and from server.
        //    will be same in host mode.
        protected void CreateNetworkedAndSpawnPlayer<T>(
            out GameObject serverGO, out NetworkIdentity serverIdentity, out T serverComponent,
            out GameObject clientGO, out NetworkIdentity clientIdentity, out T clientComponent,
            NetworkConnectionToClient ownerConnection)
            where T : NetworkBehaviour
        {
            // server & client need to be active before spawning
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            // create one on server, one on client
            // (spawning has to find it on client, it doesn't create it)
            CreateNetworked(out serverGO, out serverIdentity, out serverComponent);
            CreateNetworked(out clientGO, out clientIdentity, out clientComponent);

            // give both a scene id and register it on client for spawnables
            clientIdentity.sceneId = serverIdentity.sceneId = (ulong)serverGO.GetHashCode();
            NetworkClient.spawnableObjects[clientIdentity.sceneId] = clientIdentity;

            // IMPORTANT: OnSpawn finds 'sceneId' in .spawnableObjects.
            // only those who are ConsiderForSpawn() are in there.
            // for scene objects to be considered, they need to be disabled.
            // (it'll be active by the time we return here)
            clientGO.SetActive(false);

            // add as player & process spawn message on client.
            NetworkServer.AddPlayerForConnection(ownerConnection, serverGO);
            ProcessMessages();

            // double check isServer/isClient. avoids debugging headaches.
            Assert.That(serverIdentity.isServer, Is.True);
            Assert.That(clientIdentity.isClient, Is.True);

            // make sure the client really spawned it.
            Assert.That(clientGO.activeSelf, Is.True);
            Assert.That(NetworkClient.spawned.ContainsKey(serverIdentity.netId));
        }

        // create GameObject + NetworkIdentity + NetworkBehaviours & SPAWN PLAYER.
        // often times, we really need a player object for the client to receive
        // certain messages.
        // => returns objects from client and from server.
        //    will be same in host mode.
        protected void CreateNetworkedAndSpawnPlayer<T, U>(
            out GameObject serverGO, out NetworkIdentity serverIdentity, out T serverComponentA, out U serverComponentB,
            out GameObject clientGO, out NetworkIdentity clientIdentity, out T clientComponentA, out U clientComponentB,
            NetworkConnectionToClient ownerConnection)
            where T : NetworkBehaviour
            where U : NetworkBehaviour
        {
            // server & client need to be active before spawning
            Debug.Assert(NetworkClient.active, "NetworkClient needs to be active before spawning.");
            Debug.Assert(NetworkServer.active, "NetworkServer needs to be active before spawning.");

            // create one on server, one on client
            // (spawning has to find it on client, it doesn't create it)
            CreateNetworked(out serverGO, out serverIdentity, out serverComponentA, out serverComponentB);
            CreateNetworked(out clientGO, out clientIdentity, out clientComponentA, out clientComponentB);

            // give both a scene id and register it on client for spawnables
            clientIdentity.sceneId = serverIdentity.sceneId = (ulong)serverGO.GetHashCode();
            NetworkClient.spawnableObjects[clientIdentity.sceneId] = clientIdentity;

            // IMPORTANT: OnSpawn finds 'sceneId' in .spawnableObjects.
            // only those who are ConsiderForSpawn() are in there.
            // for scene objects to be considered, they need to be disabled.
            // (it'll be active by the time we return here)
            clientGO.SetActive(false);

            // add as player & process spawn message on client.
            NetworkServer.AddPlayerForConnection(ownerConnection, serverGO);
            ProcessMessages();

            // double check isServer/isClient. avoids debugging headaches.
            Assert.That(serverIdentity.isServer, Is.True);
            Assert.That(clientIdentity.isClient, Is.True);

            // make sure the client really spawned it.
            Assert.That(clientGO.activeSelf, Is.True);
            Assert.That(NetworkClient.spawned.ContainsKey(serverIdentity.netId));
        }

        // fully connect client to local server
        // gives out the server's connection to client for convenience if needed
        protected void ConnectClientBlocking(out NetworkConnectionToClient connectionToClient)
        {
            NetworkClient.Connect("127.0.0.1");
            UpdateTransport();

            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));
            connectionToClient = NetworkServer.connections.Values.First();

            // set isSpawnFinished flag.
            // so that any Spawn() calls will call OnStartClient and set isClient=true
            // for the client objects.
            // otherwise this would only happen after AddPlayerForConnection.
            // but not all tests have a player.
            NetworkClient.isSpawnFinished = true;
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

        // fully connect HOST client to local server
        // sets NetworkServer.localConnection / NetworkClient.connection.
        protected void ConnectHostClientBlocking()
        {
            NetworkClient.ConnectHost();
            HostMode.InvokeOnConnected();
            UpdateTransport();
            Assert.That(NetworkServer.connections.Count, Is.EqualTo(1));

            // set isSpawnFinished flag.
            // so that any Spawn() calls will call OnStartClient and set isClient=true
            // for the client objects.
            // otherwise this would only happen after AddPlayerForConnection.
            // but not all tests have a player.
            NetworkClient.isSpawnFinished = true;
        }

        // fully connect client to local server & authenticate & set read
        protected void ConnectHostClientBlockingAuthenticatedAndReady()
        {
            ConnectHostClientBlocking();

            // authenticate server & client connections
            NetworkServer.localConnection.isAuthenticated = true;
            NetworkClient.connection.isAuthenticated = true;

            // set ready
            NetworkClient.Ready();
            ProcessMessages();
            Assert.That(NetworkServer.localConnection.isReady, Is.True);
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
    }
}
