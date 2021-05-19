using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.RemoteAttrributeTest
{
    public class RemoteTestBase : MirrorTest
    {
        protected List<GameObject> spawned = new List<GameObject>();

        [SetUp]
        public void Setup()
        {
            // start server/client
            NetworkServer.Listen(1);
            NetworkClient.ConnectHost();
            NetworkServer.SpawnObjects();
            NetworkServer.ActivateHostScene();
            NetworkClient.ConnectLocalServer();

            NetworkServer.localConnection.isAuthenticated = true;
            NetworkClient.connection.isAuthenticated = true;

            NetworkClient.Ready();
        }

        [TearDown]
        public override void TearDown()
        {
            // stop server/client
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

            base.TearDown();
        }
    }
}
