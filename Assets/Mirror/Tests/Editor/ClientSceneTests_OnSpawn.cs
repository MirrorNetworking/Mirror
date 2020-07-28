using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.ClientSceneTests
{
    public class FakeNetworkConnection : NetworkConnectionToClient
    {
        public FakeNetworkConnection() : base(1)
        {
        }

        public override string address => "Test";

        public override void Disconnect()
        {
            // nothing
        }

        internal override bool Send(ArraySegment<byte> segment, int channelId = 0)
        {
            return true;
        }
    }

    public class PayloadTestBehaviour : NetworkBehaviour
    {
        public int value;
        public Vector3 direction;

        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            base.OnSerialize(writer, initialState);

            writer.WriteInt32(value);
            writer.WriteVector3(direction);

            return true;
        }
        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            base.OnDeserialize(reader, initialState);

            value = reader.ReadInt32();
            direction = reader.ReadVector3();
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

    public class ClientSceneTests_OnSpawn : ClientSceneTestsBase
    {
        Dictionary<uint, NetworkIdentity> spawned => NetworkIdentity.spawned;

        [TearDown]
        public override void TearDown()
        {
            spawned.Clear();
            base.TearDown();
        }


        [Test]
        public void FindOrSpawnObject_FindExistingObject()
        {
            const uint netId = 1000;
            GameObject go = new GameObject();
            NetworkIdentity existing = go.AddComponent<NetworkIdentity>();
            existing.netId = netId;
            spawned.Add(netId, existing);

            SpawnMessage msg = new SpawnMessage
            {
                netId = netId
            };
            bool success = ClientScene.FindOrSpawnObject(msg, out NetworkIdentity found);

            Assert.IsTrue(success);
            Assert.That(found, Is.EqualTo(existing));


            // cleanup
            GameObject.DestroyImmediate(found.gameObject);
        }

        [Test]
        public void FindOrSpawnObject_ErrorWhenNoExistingAndAssetIdAndSceneIdAreBothEmpty()
        {
            const uint netId = 1001;
            SpawnMessage msg = new SpawnMessage
            {
                assetId = new Guid(),
                sceneId = 0,
                netId = netId
            };

            LogAssert.Expect(LogType.Error, $"OnSpawn message with netId '{netId}' has no AssetId or sceneId");
            bool success = ClientScene.FindOrSpawnObject(msg, out NetworkIdentity networkIdentity);

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
                assetId = validPrefabGuid

            };

            prefabs.Add(validPrefabGuid, validPrefab);

            bool success = ClientScene.FindOrSpawnObject(msg, out NetworkIdentity networkIdentity);

            Assert.IsTrue(success);
            Assert.IsNotNull(networkIdentity);
            Assert.That(networkIdentity.name, Is.EqualTo(validPrefab.name + "(Clone)"));

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
                assetId = validPrefabGuid
            };

            // could happen if the prefab is destroyed or unloaded
            prefabs.Add(validPrefabGuid, null);

            LogAssert.Expect(LogType.Error, $"Prefab in dictionary was null for assetId '{msg.assetId}'. If you delete or unload the prefab make sure to unregister it from ClientScene too.");
            LogAssert.Expect(LogType.Error, $"Failed to spawn server object, did you forget to add it to the NetworkManager? assetId={msg.assetId} netId={msg.netId}");
            LogAssert.Expect(LogType.Error, $"Could not spawn assetId={msg.assetId} scene={msg.sceneId} netId={msg.netId}");
            bool success = ClientScene.FindOrSpawnObject(msg, out NetworkIdentity networkIdentity);


            Assert.IsFalse(success);
            Assert.IsNull(networkIdentity);
        }

        [Test]
        public void FindOrSpawnObject_SpawnsFromPrefabIfBothPrefabAndHandlerExists()
        {
            const uint netId = 1003;
            int handlerCalled = 0;
            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                assetId = validPrefabGuid
            };

            prefabs.Add(validPrefabGuid, validPrefab);
            spawnHandlers.Add(validPrefabGuid, (x) =>
            {
                handlerCalled++;
                GameObject go = new GameObject("testObj", typeof(NetworkIdentity));
                _createdObjects.Add(go);
                return go;
            });


            bool success = ClientScene.FindOrSpawnObject(msg, out NetworkIdentity networkIdentity);

            Assert.IsTrue(success);
            Assert.IsNotNull(networkIdentity);
            Assert.That(networkIdentity.name, Is.EqualTo(validPrefab.name + "(Clone)"));
            Assert.That(handlerCalled, Is.EqualTo(0), "Handler should not have been called");
        }

        [Test]
        public void FindOrSpawnObject_SpawnHandlerCalledFromDictionary()
        {
            const uint netId = 1003;
            int handlerCalled = 0;
            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                assetId = validPrefabGuid
            };

            GameObject createdInhandler = null;

            spawnHandlers.Add(validPrefabGuid, (x) =>
            {
                handlerCalled++;
                Assert.That(x, Is.EqualTo(msg));
                createdInhandler = new GameObject("testObj", typeof(NetworkIdentity));
                _createdObjects.Add(createdInhandler);
                return createdInhandler;
            });


            bool success = ClientScene.FindOrSpawnObject(msg, out NetworkIdentity networkIdentity);

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
                assetId = validPrefabGuid
            };

            spawnHandlers.Add(validPrefabGuid, (x) =>
            {
                return null;
            });

            LogAssert.Expect(LogType.Error, $"Spawn Handler returned null, Handler assetId '{msg.assetId}'");
            LogAssert.Expect(LogType.Error, $"Could not spawn assetId={msg.assetId} scene={msg.sceneId} netId={msg.netId}");
            bool success = ClientScene.FindOrSpawnObject(msg, out NetworkIdentity networkIdentity);

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
                assetId = validPrefabGuid
            };

            spawnHandlers.Add(validPrefabGuid, (x) =>
            {
                GameObject go = new GameObject("testObj");
                _createdObjects.Add(go);
                return go;
            });

            LogAssert.Expect(LogType.Error, $"Object Spawned by handler did not have a NetworkIdentity, Handler assetId '{validPrefabGuid}'");
            LogAssert.Expect(LogType.Error, $"Could not spawn assetId={msg.assetId} scene={msg.sceneId} netId={msg.netId}");
            bool success = ClientScene.FindOrSpawnObject(msg, out NetworkIdentity networkIdentity);

            Assert.IsFalse(success);
            Assert.IsNull(networkIdentity);
        }

        NetworkIdentity CreateSceneObject(ulong sceneId)
        {
            GameObject runtimeObject = new GameObject("Runtime GameObject");
            NetworkIdentity networkIdentity = runtimeObject.AddComponent<NetworkIdentity>();
            // set sceneId to zero as it is set in onvalidate (does not set id at runtime)
            networkIdentity.sceneId = sceneId;

            _createdObjects.Add(runtimeObject);
            spawnableObjects.Add(sceneId, networkIdentity);

            return networkIdentity;
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


            bool success = ClientScene.FindOrSpawnObject(msg, out NetworkIdentity networkIdentity);

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
                assetId = validPrefabGuid
            };

            prefabs.Add(validPrefabGuid, validPrefab);
            NetworkIdentity sceneObject = CreateSceneObject(sceneId);

            bool success = ClientScene.FindOrSpawnObject(msg, out NetworkIdentity networkIdentity);

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

            LogAssert.Expect(LogType.Error, $"Spawn scene object not found for {msg.sceneId.ToString("X")} SpawnableObjects.Count={spawnableObjects.Count}");
            LogAssert.Expect(LogType.Error, $"Could not spawn assetId={msg.assetId} scene={msg.sceneId} netId={msg.netId}");
            bool success = ClientScene.FindOrSpawnObject(msg, out NetworkIdentity networkIdentity);

            Assert.IsFalse(success);
            Assert.IsNull(networkIdentity);
        }


        [Test]
        public void ApplyPayload_AppliesTransform()
        {
            const uint netId = 1000;

            GameObject go = new GameObject();
            _createdObjects.Add(go);

            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();

            Vector3 position = new Vector3(10, 0, 20);
            Quaternion rotation = Quaternion.Euler(0, 45, 0);
            Vector3 scale = new Vector3(1.5f, 1.5f, 1.5f);
            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                isLocalPlayer = false,
                isOwner = false,
                sceneId = 0,
                assetId = Guid.Empty,
                // use local values for VR support
                position = position,
                rotation = rotation,
                scale = scale,

                payload = default,
            };

            ClientScene.ApplySpawnPayload(identity, msg);

            Assert.That(identity.transform.position, Is.EqualTo(position));
            // use angle because of floating point numbers
            // only need to check if rotations are approximatly equal
            Assert.That(Quaternion.Angle(identity.transform.rotation, rotation), Is.LessThan(0.0001f));
            Assert.That(identity.transform.localScale, Is.EqualTo(scale));
        }

        [Test]
        public void ApplyPayload_AppliesLocalValuesToTransform()
        {
            const uint netId = 1000;
            GameObject parent = new GameObject();
            _createdObjects.Add(parent);
            parent.transform.position = new Vector3(100, 20, 0);
            parent.transform.rotation = Quaternion.LookRotation(Vector3.left);
            parent.transform.localScale = Vector3.one * 2;

            GameObject go = new GameObject();
            go.transform.parent = parent.transform;
            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();

            Vector3 position = new Vector3(10, 0, 20);
            Quaternion rotation = Quaternion.Euler(0, 45, 0);
            Vector3 scale = new Vector3(1.5f, 1.5f, 1.5f);
            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                isLocalPlayer = false,
                isOwner = false,
                sceneId = 0,
                assetId = Guid.Empty,
                // use local values for VR support
                position = position,
                rotation = rotation,
                scale = scale,

                payload = default,
            };

            ClientScene.ApplySpawnPayload(identity, msg);

            Assert.That(identity.transform.localPosition, Is.EqualTo(position));
            // use angle because of floating point numbers
            // only need to check if rotations are approximatly equal
            Assert.That(Quaternion.Angle(identity.transform.localRotation, rotation), Is.LessThan(0.0001f));
            Assert.That(identity.transform.localScale, Is.EqualTo(scale));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void ApplyPayload_AppliesAuthority(bool isOwner)
        {
            const uint netId = 1000;

            GameObject go = new GameObject();
            _createdObjects.Add(go);

            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();

            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                isLocalPlayer = false,
                isOwner = isOwner,
                sceneId = 0,
                assetId = Guid.Empty,
                // use local values for VR support
                position = Vector3.zero,
                rotation = Quaternion.identity,
                scale = Vector3.one,

                payload = default,
            };

            // set to oposite to make sure it is changed
            identity.hasAuthority = !isOwner;

            ClientScene.ApplySpawnPayload(identity, msg);

            Assert.That(identity.hasAuthority, Is.EqualTo(isOwner));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void ApplyPayload_EnablesObject(bool startActive)
        {
            const uint netId = 1000;

            GameObject go = new GameObject();
            _createdObjects.Add(go);

            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();
            go.SetActive(startActive);

            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                isLocalPlayer = false,
                isOwner = false,
                sceneId = 0,
                assetId = Guid.Empty,
                // use local values for VR support
                position = Vector3.zero,
                rotation = Quaternion.identity,
                scale = Vector3.one,

                payload = default,
            };

            ClientScene.ApplySpawnPayload(identity, msg);

            Assert.IsTrue(identity.gameObject.activeSelf);
        }

        [Test]
        public void ApplyPayload_SetsAssetId()
        {
            const uint netId = 1000;

            GameObject go = new GameObject();
            _createdObjects.Add(go);

            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();

            Guid guid = Guid.NewGuid();
            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                isLocalPlayer = false,
                isOwner = false,
                sceneId = 0,
                assetId = guid,
                // use local values for VR support
                position = Vector3.zero,
                rotation = Quaternion.identity,
                scale = Vector3.one,

                payload = default,
            };

            ClientScene.ApplySpawnPayload(identity, msg);

            Assert.IsTrue(identity.gameObject.activeSelf);

            Assert.That(identity.assetId, Is.EqualTo(guid));
        }

        [Test]
        public void ApplyPayload_DoesNotSetAssetIdToEmpty()
        {
            const uint netId = 1000;

            GameObject go = new GameObject();
            _createdObjects.Add(go);

            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();
            Guid guid = Guid.NewGuid();
            identity.assetId = guid;

            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                isLocalPlayer = false,
                isOwner = false,
                sceneId = 0,
                assetId = Guid.Empty,
                // use local values for VR support
                position = Vector3.zero,
                rotation = Quaternion.identity,
                scale = Vector3.one,

                payload = default,
            };

            ClientScene.ApplySpawnPayload(identity, msg);

            Assert.That(identity.assetId, Is.EqualTo(guid), "AssetId should not have changed");
        }

        [Test]
        public void ApplyPayload_SendsDataToNetworkBehaviourDeserialize()
        {
            Assert.Ignore();


            // use PayloadTestBehaviour
        }

        [Test]
        public void ApplyPayload_LocalPlayerAddsIdentityToConnection()
        {
            Debug.Assert(ClientScene.localPlayer == null, "LocalPlayer should be null before this test");
            const uint netId = 1000;

            GameObject go = new GameObject();
            _createdObjects.Add(go);

            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();

            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                isLocalPlayer = true,
                isOwner = true,
                sceneId = 0,
                assetId = Guid.Empty,
                // use local values for VR support
                position = Vector3.zero,
                rotation = Quaternion.identity,
                scale = Vector3.one,

                payload = default,
            };

            PropertyInfo readyConnProperty = typeof(ClientScene).GetProperty(nameof(ClientScene.readyConnection));
            readyConnProperty.SetValue(null, new FakeNetworkConnection());

            ClientScene.ApplySpawnPayload(identity, msg);

            Assert.That(ClientScene.localPlayer, Is.EqualTo(identity));
            Assert.That(ClientScene.readyConnection.identity, Is.EqualTo(identity));
        }

        [Test]
        public void ApplyPayload_LocalPlayerWarningWhenNoReadyConnection()
        {
            Debug.Assert(ClientScene.localPlayer == null, "LocalPlayer should be null before this test");
            const uint netId = 1000;

            GameObject go = new GameObject();
            _createdObjects.Add(go);

            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();

            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                isLocalPlayer = true,
                isOwner = true,
                sceneId = 0,
                assetId = Guid.Empty,
                // use local values for VR support
                position = Vector3.zero,
                rotation = Quaternion.identity,
                scale = Vector3.one,

                payload = default,
            };


            LogAssert.Expect(LogType.Warning, "No ready connection found for setting player controller during InternalAddPlayer");
            ClientScene.ApplySpawnPayload(identity, msg);

            Assert.That(ClientScene.localPlayer, Is.EqualTo(identity));
        }

        [Flags]
        public enum SpawnFinishedState
        {
            isSpawnFinished = 1,
            hasAuthority = 2,
            isLocalPlayer = 4
        }
        [Test]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        public void ApplyPayload_isSpawnFinished(SpawnFinishedState flag)
        {
            bool isSpawnFinished = flag.HasFlag(SpawnFinishedState.isSpawnFinished);
            bool hasAuthority = flag.HasFlag(SpawnFinishedState.hasAuthority);
            bool isLocalPlayer = flag.HasFlag(SpawnFinishedState.isLocalPlayer);

            if (isSpawnFinished)
            {
                ClientScene.OnObjectSpawnFinished(new ObjectSpawnFinishedMessage { });
            }

            const uint netId = 1000;
            GameObject go = new GameObject();
            _createdObjects.Add(go);

            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();
            BehaviourWithEvents events = go.AddComponent<BehaviourWithEvents>();

            int onStartAuthorityCalled = 0;
            int onStartClientCalled = 0;
            int onStartLocalPlayerCalled = 0;
            events.OnStartAuthorityCalled += () => { onStartAuthorityCalled++; };
            events.OnStartClientCalled += () => { onStartClientCalled++; };
            events.OnStartLocalPlayerCalled += () => { onStartLocalPlayerCalled++; };

            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                isLocalPlayer = isLocalPlayer,
                isOwner = hasAuthority,
            };

            ClientScene.ApplySpawnPayload(identity, msg);

            if (isSpawnFinished)
            {
                Assert.That(onStartClientCalled, Is.EqualTo(1));
                Assert.That(onStartAuthorityCalled, Is.EqualTo(hasAuthority ? 1 : 0));
                Assert.That(onStartLocalPlayerCalled, Is.EqualTo(isLocalPlayer ? 1 : 0));
            }
            else
            {
                Assert.That(onStartAuthorityCalled, Is.Zero);
                Assert.That(onStartClientCalled, Is.Zero);
                Assert.That(onStartLocalPlayerCalled, Is.Zero);
            }
        }


        [Test]
        public void OnSpawn_SpawnsAndAppliesPayload()
        {
            const int netId = 1;
            Debug.Assert(spawned.Count == 0, "There should be no spawned objects before test");


            Vector3 position = new Vector3(30, 20, 10);
            Quaternion rotation = Quaternion.Euler(0, 0, 90);
            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                assetId = validPrefabGuid,
                position = position,
                rotation = rotation
            };
            prefabs.Add(validPrefabGuid, validPrefab);

            ClientScene.OnSpawn(msg);

            Assert.That(spawned.Count, Is.EqualTo(1));
            Assert.IsTrue(spawned.ContainsKey(netId));

            NetworkIdentity identity = spawned[netId];
            Assert.IsNotNull(identity);
            Assert.That(identity.name, Is.EqualTo(validPrefab.name + "(Clone)"));
            Assert.That(identity.transform.position, Is.EqualTo(position));
            // use angle because of floating point numbers
            // only need to check if rotations are approximatly equal
            Assert.That(Quaternion.Angle(identity.transform.rotation, rotation), Is.LessThan(0.0001f));
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
            ClientScene.OnSpawn(msg);

            Assert.That(spawned, Is.Empty);
        }
    }
}
