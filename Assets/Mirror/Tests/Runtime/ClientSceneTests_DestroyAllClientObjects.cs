using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime.ClientSceneTests
{
    public class TestListenerBehaviour : NetworkBehaviour
    {
        // If object is destroyed then both OnDisable and OnDestroy will be called
        public event Action onDestroyCalled;
        public event Action onDisableCalled;

        public void OnDisable() => onDisableCalled?.Invoke();
        void OnDestroy() => onDestroyCalled?.Invoke();
    }

    // A network Behaviour that changes NetworkIdentity.spawned in OnDisable
    public class BadBehaviour : NetworkBehaviour
    {
        public void OnDisable()
        {
            GameObject go = new GameObject();
            NetworkIdentity netId = go.AddComponent<NetworkIdentity>();
            const int id = 32032;
            netId.netId = id;

            NetworkClient.spawned.Add(id, netId);
        }
    }

    public class ClientSceneTests_DestroyAllClientObjects : MirrorPlayModeTest
    {
        Dictionary<uint, UnSpawnDelegate> unspawnHandlers => NetworkClient.unspawnHandlers;

        NetworkConnectionToClient connectionToClient;

        [UnitySetUp]
        public override IEnumerator UnitySetUp()
        {
            yield return base.UnitySetUp();

            // start server & client and wait 1 frame
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out connectionToClient);
            yield return null;
        }

        [UnityTearDown]
        public override IEnumerator UnityTearDown()
        {
            unspawnHandlers.Clear();
            base.TearDown();
            yield return null;
        }

        void CreateAndAddObject(ulong sceneId, out TestListenerBehaviour serverComp, out TestListenerBehaviour clientComp)
        {
            CreateNetworkedAndSpawnPlayer(out _, out NetworkIdentity serverIdentity, out serverComp,
                                          out _, out NetworkIdentity clientIdentity, out clientComp,
                                          connectionToClient);
            serverIdentity.sceneId = clientIdentity.sceneId = sceneId;
        }

        [UnityTest]
        public IEnumerator DestroysAllNetworkPrefabsInScene()
        {
            // sceneId 0 is prefab
            CreateAndAddObject(0, out TestListenerBehaviour serverComp, out TestListenerBehaviour clientComp);

            int destroyCalled = 0;
            clientComp.onDestroyCalled += () => destroyCalled++;

            NetworkClient.DestroyAllClientObjects();

            // wait for frame to make sure unity events are called
            yield return null;
            Assert.That(destroyCalled, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisablesAllNetworkSceneObjectsInScene()
        {
            // sceneId 0 is prefab
            CreateAndAddObject(101, out TestListenerBehaviour serverComp, out TestListenerBehaviour clientComp);

            int disableCalled = 0;
            clientComp.onDisableCalled += () => disableCalled++;

            int destroyCalled = 0;
            clientComp.onDestroyCalled += () => destroyCalled++;

            NetworkClient.DestroyAllClientObjects();

            // wait for frame to make sure unity events are called
            yield return null;

            Assert.That(disableCalled, Is.EqualTo(1));
            Assert.That(destroyCalled, Is.EqualTo(0), "Scene objects should not be destroyed");
        }

        [Test]
        public void CallsUnspawnHandlerInsteadOfDestroy()
        {
            // sceneId 0 is prefab
            CreateAndAddObject(0, out TestListenerBehaviour serverComp, out TestListenerBehaviour clientComp);

            uint assetId1 = 1;

            int unspawnCalled = 0;
            unspawnHandlers.Add(assetId1, x => unspawnCalled++);
            clientComp.GetComponent<NetworkIdentity>().assetId = assetId1;

            int disableCalled = 0;

            clientComp.onDisableCalled += () => disableCalled++;

            NetworkClient.DestroyAllClientObjects();

            Assert.That(unspawnCalled, Is.EqualTo(1));
            Assert.That(disableCalled, Is.EqualTo(0), "Object with UnspawnHandler should not be destroyed");
        }

        [Test]
        public void ClearsSpawnedList()
        {
            // sceneId 0 is prefab
            CreateAndAddObject(0, out TestListenerBehaviour serverComp, out TestListenerBehaviour clientComp);

            NetworkClient.DestroyAllClientObjects();

            Assert.That(NetworkClient.spawned, Is.Empty);
        }

        [Test]
        public void CatchesAndLogsExeptionWhenSpawnedListIsChanged()
        {
            // create spawned (needs to be added to .spawned!)
            CreateNetworkedAndSpawn(out _, out _, out BadBehaviour serverComp,
                                    out _, out _, out BadBehaviour clientComp);

            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException"));
            LogAssert.Expect(LogType.Error, "Could not DestroyAllClientObjects because spawned list was modified during loop, make sure you are not modifying NetworkIdentity.spawned by calling NetworkServer.Destroy or NetworkServer.Spawn in OnDestroy or OnDisable.");
            NetworkClient.DestroyAllClientObjects();
        }
    }
}
