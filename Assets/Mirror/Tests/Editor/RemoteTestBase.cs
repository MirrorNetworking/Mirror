using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.RemoteAttrributeTest
{
    public class RemoteTestBase
    {
        protected List<GameObject> spawned = new List<GameObject>();

        [SetUp]
        public void Setup()
        {
            Transport.activeTransport = new GameObject().AddComponent<MemoryTransport>();

            // start server/client
            NetworkServer.Listen(1);
            NetworkClient.ConnectHost();
            NetworkServer.SpawnObjects();
            NetworkServer.ActivateHostScene();
            NetworkClient.ConnectLocalServer();

            NetworkServer.localConnection.isAuthenticated = true;
            NetworkClient.connection.isAuthenticated = true;

            ClientScene.Ready(NetworkClient.connection);
        }

        [TearDown]
        public void TearDown()
        {
            // stop server/client
            NetworkClient.DisconnectLocalServer();

            NetworkClient.Disconnect();
            NetworkClient.Shutdown();

            NetworkServer.Shutdown();

            // destroy left over objects
            foreach (GameObject item in spawned)
            {
                if (item != null)
                {
                    GameObject.DestroyImmediate(item);
                }
            }

            spawned.Clear();

            NetworkIdentity.spawned.Clear();

            GameObject.DestroyImmediate(Transport.activeTransport.gameObject);
        }

        protected T CreateHostObject<T>(bool spawnWithAuthority) where T : NetworkBehaviour
        {
            GameObject gameObject = new GameObject();
            spawned.Add(gameObject);

            gameObject.AddComponent<NetworkIdentity>();

            T behaviour = gameObject.AddComponent<T>();

            // spawn outwith authority
            if (spawnWithAuthority)
            {
                NetworkServer.Spawn(gameObject, NetworkServer.localConnection);
                Debug.Assert(behaviour.connectionToClient != null, $"Behaviour did not have connection to client, This means that the test is broken and will give the wrong results");
            }
            else
            {
                NetworkServer.Spawn(gameObject);
            }
            ProcessMessages();

            Debug.Assert(behaviour.hasAuthority == spawnWithAuthority, $"Behaviour Had Wrong Authority when spawned, This means that the test is broken and will give the wrong results");

            return behaviour;
        }

        protected static void ProcessMessages()
        {
            // run update so message are processed
            NetworkServer.NetworkLateUpdate();
            NetworkClient.NetworkLateUpdate();
        }
    }
}
