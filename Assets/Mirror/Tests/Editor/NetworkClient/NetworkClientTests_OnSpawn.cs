using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.NetworkClients
{
    public class PayloadTestBehaviour : NetworkBehaviour
    {
        public int value;
        public Vector3 direction;

        public event Action OnDeserializeCalled;
        public event Action OnSerializeCalled;

        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            base.OnSerialize(writer, initialState);

            writer.WriteInt(value);
            writer.WriteVector3(direction);

            OnSerializeCalled?.Invoke();
        }
        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            base.OnDeserialize(reader, initialState);

            value = reader.ReadInt();
            direction = reader.ReadVector3();

            OnDeserializeCalled?.Invoke();
        }
    }

    public class BehaviourWithEvents : NetworkBehaviour
    {
        public event Action OnStartAuthorityCalled;
        public event Action OnStartClientCalled;
        public event Action OnStartLocalPlayerCalled;

        public override void OnStartAuthority()
        {
            OnStartAuthorityCalled?.Invoke();
        }
        public override void OnStartClient()
        {
            OnStartClientCalled?.Invoke();
        }
        public override void OnStartLocalPlayer()
        {
            OnStartLocalPlayerCalled?.Invoke();
        }
    }

    public class NetworkClientTests_OnSpawn : NetworkClientTestsBase
    {
        Dictionary<uint, NetworkIdentity> spawned => NetworkClient.spawned;

        [TearDown]
        public override void TearDown()
        {
            spawned.Clear();
            base.TearDown();
        }

        [Test]
        public void FindOrSpawnObject_FindExistingObject()
        {
            CreateNetworked(out _, out NetworkIdentity existing);
            const uint netId = 1000;
            existing.netId = netId;
            spawned.Add(netId, existing);

            SpawnMessage msg = new SpawnMessage
            {
                netId = netId
            };
            bool success = NetworkClient.FindOrSpawnObject(msg, out NetworkIdentity found);

            Assert.IsTrue(success);
            Assert.That(found, Is.EqualTo(existing));
        }

        [Test]
        public void FindOrSpawnObject_ErrorWhenNoExistingAndAssetIdAndSceneIdAreBothEmpty()
        {
            const uint netId = 1001;
            SpawnMessage msg = new SpawnMessage
            {
                assetId = 0,
                sceneId = 0,
                netId = netId
            };

            LogAssert.Expect(LogType.Error, $"OnSpawn message with netId '{netId}' has no AssetId or sceneId");
            bool success = NetworkClient.FindOrSpawnObject(msg, out NetworkIdentity networkIdentity);

            Assert.IsFalse(success);
            Assert.IsNull(networkIdentity);
        }

        [Test]
        public void FindOrSpawnObject_SpawnsFromPrefabDictionary()
        {
            const uint netId = 1002;
            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                assetId = validPrefabAssetId

            };

            NetworkClient.prefabs.Add(validPrefabAssetId, validPrefab);

            bool success = NetworkClient.FindOrSpawnObject(msg, out NetworkIdentity networkIdentity);

            Assert.IsTrue(success);
            Assert.IsNotNull(networkIdentity);
            Assert.That(networkIdentity.name, Is.EqualTo($"{validPrefab.name}(Clone)"));

            // cleanup
            GameObject.DestroyImmediate(networkIdentity.gameObject);
        }

        [Test]
        public void FindOrSpawnObject_ErrorWhenPrefabInNullInDictionary()
        {
            const uint netId = 1002;
            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                assetId = validPrefabAssetId
            };

            // could happen if the prefab is destroyed or unloaded
            NetworkClient.prefabs.Add(validPrefabAssetId, null);

            LogAssert.Expect(LogType.Error, $"Failed to spawn server object, did you forget to add it to the NetworkManager? assetId={msg.assetId} netId={msg.netId}");
            LogAssert.Expect(LogType.Error, $"Could not spawn assetId={msg.assetId} scene={msg.sceneId:X} netId={msg.netId}");
            bool success = NetworkClient.FindOrSpawnObject(msg, out NetworkIdentity networkIdentity);


            Assert.IsFalse(success);
            Assert.IsNull(networkIdentity);
        }

        // test to prevent https://github.com/vis2k/Mirror/issues/2705
        [Test]
        public void FindOrSpawnObject_SpawnsFromHandlerIfBothPrefabAndHandlerExists()
        {
            const uint netId = 1003;
            int handlerCalled = 0;
            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                assetId = validPrefabAssetId
            };

            NetworkClient.prefabs.Add(validPrefabAssetId, validPrefab);
            NetworkClient.spawnHandlers.Add(validPrefabAssetId, x =>
            {
                handlerCalled++;
                CreateNetworked(out GameObject go, out NetworkIdentity _);
                return go;
            });

            bool success = NetworkClient.FindOrSpawnObject(msg, out NetworkIdentity networkIdentity);

            Assert.IsTrue(success);
            Assert.IsNotNull(networkIdentity);
            Assert.That(handlerCalled, Is.EqualTo(1));
        }

        [Test]
        public void FindOrSpawnObject_SpawnHandlerCalledFromDictionary()
        {
            const uint netId = 1003;
            int handlerCalled = 0;
            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                assetId = validPrefabAssetId
            };

            GameObject createdInhandler = null;

            NetworkClient.spawnHandlers.Add(validPrefabAssetId, x =>
            {
                handlerCalled++;
                Assert.That(x, Is.EqualTo(msg));
                CreateNetworked(out createdInhandler, out NetworkIdentity _);
                return createdInhandler;
            });

            bool success = NetworkClient.FindOrSpawnObject(msg, out NetworkIdentity networkIdentity);

            Assert.IsTrue(success);
            Assert.IsNotNull(networkIdentity);
            Assert.That(handlerCalled, Is.EqualTo(1));
            Assert.That(networkIdentity.gameObject, Is.EqualTo(createdInhandler), "Object returned should be the same object created by the spawn handler");
        }

        [Test]
        public void FindOrSpawnObject_ErrorWhenSpawnHanlderReturnsNull()
        {
            const uint netId = 1003;
            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                assetId = validPrefabAssetId
            };

            NetworkClient.spawnHandlers.Add(validPrefabAssetId, (x) => null);

            LogAssert.Expect(LogType.Error, $"Spawn Handler returned null, Handler assetId '{msg.assetId}'");
            LogAssert.Expect(LogType.Error, $"Could not spawn assetId={msg.assetId} scene={msg.sceneId:X} netId={msg.netId}");
            bool success = NetworkClient.FindOrSpawnObject(msg, out NetworkIdentity networkIdentity);

            Assert.IsFalse(success);
            Assert.IsNull(networkIdentity);
        }
        [Test]
        public void FindOrSpawnObject_ErrorWhenSpawnHanlderReturnsWithoutNetworkIdentity()
        {
            const uint netId = 1003;
            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                assetId = validPrefabAssetId
            };

            NetworkClient.spawnHandlers.Add(validPrefabAssetId, (x) =>
            {
                CreateGameObject(out GameObject go);
                return go;
            });

            LogAssert.Expect(LogType.Error, $"Object Spawned by handler did not have a NetworkIdentity, Handler assetId '{validPrefabAssetId}'");
            LogAssert.Expect(LogType.Error, $"Could not spawn assetId={msg.assetId} scene={msg.sceneId:X} netId={msg.netId}");
            bool success = NetworkClient.FindOrSpawnObject(msg, out NetworkIdentity networkIdentity);

            Assert.IsFalse(success);
            Assert.IsNull(networkIdentity);
        }

        NetworkIdentity CreateSceneObject(ulong sceneId)
        {
            CreateNetworked(out _, out NetworkIdentity identity);
            // set sceneId to zero as it is set in onvalidate (does not set id at runtime)
            identity.sceneId = sceneId;
            NetworkClient.spawnableObjects.Add(sceneId, identity);
            return identity;
        }

        [Test]
        public void FindOrSpawnObject_UsesSceneIdToSpawnFromSpawnableObjectsDictionary()
        {
            const uint netId = 1003;
            const int sceneId = 100020;
            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                sceneId = sceneId
            };

            NetworkIdentity sceneObject = CreateSceneObject(sceneId);


            bool success = NetworkClient.FindOrSpawnObject(msg, out NetworkIdentity networkIdentity);

            Assert.IsTrue(success);
            Assert.IsNotNull(networkIdentity);
            Assert.That(networkIdentity, Is.EqualTo(sceneObject));
        }

        [Test]
        public void FindOrSpawnObject_SpawnsUsingSceneIdInsteadOfAssetId()
        {
            const uint netId = 1003;
            const int sceneId = 100020;
            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                sceneId = sceneId,
                assetId = validPrefabAssetId
            };

            NetworkClient.prefabs.Add(validPrefabAssetId, validPrefab);
            NetworkIdentity sceneObject = CreateSceneObject(sceneId);

            bool success = NetworkClient.FindOrSpawnObject(msg, out NetworkIdentity networkIdentity);

            Assert.IsTrue(success);
            Assert.IsNotNull(networkIdentity);
            Assert.That(networkIdentity, Is.EqualTo(sceneObject));
        }

        [Test]
        public void FindOrSpawnObject_ErrorWhenSceneIdIsNotInSpawnableObjectsDictionary()
        {
            const uint netId = 1004;
            const int sceneId = 100021;
            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                sceneId = sceneId,
            };

            LogAssert.Expect(LogType.Error, $"Spawn scene object not found for {msg.sceneId:X}. Make sure that client and server use exactly the same project. This only happens if the hierarchy gets out of sync.");
            LogAssert.Expect(LogType.Error, $"Could not spawn assetId={msg.assetId} scene={msg.sceneId:X} netId={msg.netId}");
            bool success = NetworkClient.FindOrSpawnObject(msg, out NetworkIdentity networkIdentity);

            Assert.IsFalse(success);
            Assert.IsNull(networkIdentity);
        }


        [Test]
        public void ApplyPayload_AppliesTransform()
        {
            const uint netId = 1000;

            CreateNetworked(out GameObject _, out NetworkIdentity identity);

            Vector3 position = new Vector3(10, 0, 20);
            Quaternion rotation = Quaternion.Euler(0, 45, 0);
            Vector3 scale = new Vector3(1.5f, 1.5f, 1.5f);
            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                isLocalPlayer = false,
                isOwner = false,
                sceneId = 0,
                assetId = 0,
                // use local values for VR support
                position = position,
                rotation = rotation,
                scale = scale,

                payload = default,
            };

            NetworkClient.ApplySpawnPayload(identity, msg);

            Assert.That(identity.transform.position, Is.EqualTo(position));
            // use angle because of floating point numbers
            // only need to check if rotations are approximately equal
            Assert.That(Quaternion.Angle(identity.transform.rotation, rotation), Is.LessThan(0.0001f));
            Assert.That(identity.transform.localScale, Is.EqualTo(scale));
        }

        [Test]
        public void ApplyPayload_AppliesLocalValuesToTransform()
        {
            const uint netId = 1000;
            CreateGameObject(out GameObject parent);
            parent.transform.position = new Vector3(100, 20, 0);
            parent.transform.rotation = Quaternion.LookRotation(Vector3.left);
            parent.transform.localScale = Vector3.one * 2;

            CreateNetworked(out GameObject go, out NetworkIdentity identity);
            go.transform.parent = parent.transform;

            Vector3 position = new Vector3(10, 0, 20);
            Quaternion rotation = Quaternion.Euler(0, 45, 0);
            Vector3 scale = new Vector3(1.5f, 1.5f, 1.5f);
            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                isLocalPlayer = false,
                isOwner = false,
                sceneId = 0,
                assetId = 0,
                // use local values for VR support
                position = position,
                rotation = rotation,
                scale = scale,

                payload = default,
            };

            NetworkClient.ApplySpawnPayload(identity, msg);

            Assert.That(identity.transform.localPosition, Is.EqualTo(position));
            // use angle because of floating point numbers
            // only need to check if rotations are approximately equal
            Assert.That(Quaternion.Angle(identity.transform.localRotation, rotation), Is.LessThan(0.0001f));
            Assert.That(identity.transform.localScale, Is.EqualTo(scale));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void ApplyPayload_AppliesAuthority(bool isOwner)
        {
            const uint netId = 1000;

            CreateNetworked(out _, out NetworkIdentity identity);

            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                isLocalPlayer = false,
                isOwner = isOwner,
                sceneId = 0,
                assetId = 0,
                // use local values for VR support
                position = Vector3.zero,
                rotation = Quaternion.identity,
                scale = Vector3.one,

                payload = default
            };

            // set to opposite to make sure it is changed
            identity.isOwned = !isOwner;

            NetworkClient.ApplySpawnPayload(identity, msg);

            Assert.That(identity.isOwned, Is.EqualTo(isOwner));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void ApplyPayload_EnablesObject(bool startActive)
        {
            const uint netId = 1000;

            CreateNetworked(out GameObject go, out NetworkIdentity identity);
            go.SetActive(startActive);

            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                isLocalPlayer = false,
                isOwner = false,
                sceneId = 0,
                assetId = 0,
                // use local values for VR support
                position = Vector3.zero,
                rotation = Quaternion.identity,
                scale = Vector3.one,

                payload = default,
            };

            NetworkClient.ApplySpawnPayload(identity, msg);

            Assert.IsTrue(identity.gameObject.activeSelf);
        }

        [Test]
        public void ApplyPayload_SetsAssetId()
        {
            const uint netId = 1000;

            CreateNetworked(out _, out NetworkIdentity identity);

            uint assetId = 42;
            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                isLocalPlayer = false,
                isOwner = false,
                sceneId = 0,
                assetId = assetId,
                // use local values for VR support
                position = Vector3.zero,
                rotation = Quaternion.identity,
                scale = Vector3.one,

                payload = default
            };

            NetworkClient.ApplySpawnPayload(identity, msg);

            Assert.IsTrue(identity.gameObject.activeSelf);

            Assert.That(identity.assetId, Is.EqualTo(assetId));
        }

        [Test]
        public void ApplyPayload_DoesNotSetAssetIdToEmpty()
        {
            const uint netId = 1000;

            CreateNetworked(out _, out NetworkIdentity identity);
            uint assetId = 42;
            identity.assetId = assetId;

            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                isLocalPlayer = false,
                isOwner = false,
                sceneId = 0,
                assetId = 0,
                // use local values for VR support
                position = Vector3.zero,
                rotation = Quaternion.identity,
                scale = Vector3.one,

                payload = default
            };

            NetworkClient.ApplySpawnPayload(identity, msg);

            Assert.That(identity.assetId, Is.EqualTo(assetId), "AssetId should not have changed");
        }

        [Test]
        public void ApplyPayload_LocalPlayerAddsIdentityToConnection()
        {
            Debug.Assert(NetworkClient.localPlayer == null, "LocalPlayer should be null before this test");
            const uint netId = 1000;

            CreateNetworked(out _, out NetworkIdentity identity);

            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                isLocalPlayer = true,
                isOwner = true,
                sceneId = 0,
                assetId = 0,
                // use local values for VR support
                position = Vector3.zero,
                rotation = Quaternion.identity,
                scale = Vector3.one,

                payload = default,
            };

            NetworkClient.connection = new FakeNetworkConnectionToServer();
            NetworkClient.ready = true;
            NetworkClient.ApplySpawnPayload(identity, msg);

            Assert.That(NetworkClient.localPlayer, Is.EqualTo(identity));
            Assert.That(NetworkClient.connection.identity, Is.EqualTo(identity));
        }

        [Test]
        public void ApplyPayload_LocalPlayerWarningWhenNoReadyConnection()
        {
            Debug.Assert(NetworkClient.localPlayer == null, "LocalPlayer should be null before this test");
            const uint netId = 1000;

            CreateNetworked(out _, out NetworkIdentity identity);

            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                isLocalPlayer = true,
                isOwner = true,
                sceneId = 0,
                assetId = 0,
                // use local values for VR support
                position = Vector3.zero,
                rotation = Quaternion.identity,
                scale = Vector3.one,

                payload = default,
            };


            LogAssert.Expect(LogType.Warning, "NetworkClient can't AddPlayer before being ready. Please call NetworkClient.Ready() first. Clients are considered ready after joining the game world.");
            NetworkClient.ApplySpawnPayload(identity, msg);

            Assert.That(NetworkClient.localPlayer, Is.EqualTo(identity));
        }

        [Flags]
        public enum SpawnFinishedState
        {
            isSpawnFinished = 1,
            hasAuthority = 2,
            isLocalPlayer = 4
        }

        [Test]
        public void OnSpawn_GiveNoExtraErrorsWhenPrefabIsntSpawned()
        {
            const int netId = 20033;
            Debug.Assert(spawned.Count == 0, "There should be no spawned objects before test");
            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
            };

            // Check for log that FindOrSpawnObject gives, and make sure there are no other error logs
            LogAssert.Expect(LogType.Error, $"OnSpawn message with netId '{netId}' has no AssetId or sceneId");
            NetworkClient.OnSpawn(msg);

            Assert.That(spawned, Is.Empty);
        }
    }
}
