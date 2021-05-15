using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.ClientSceneTests
{
    public class PayloadTestBehaviour : NetworkBehaviour
    {
        public int value;
        public Vector3 direction;

        public event Action OnDeserializeCalled;
        public event Action OnSerializeCalled;

        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            base.OnSerialize(writer, initialState);

            writer.WriteInt32(value);
            writer.WriteVector3(direction);

            OnSerializeCalled?.Invoke();

            return true;
        }
        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            base.OnDeserialize(reader, initialState);

            value = reader.ReadInt32();
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

    public class ClientSceneTests_OnSpawn : ClientSceneTestsBase
    {
        Dictionary<uint, NetworkIdentity> spawned => NetworkIdentity.spawned;

        [TearDown]
        public override void TearDown()
        {
            spawned.Clear();
            base.TearDown();
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
        public void ApplyPayload_AppliesTransform()
        {
            const uint netId = 1000;

            GameObject go = new GameObject();
            _createdObjects.Add(go);

            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();

            Vector3 position = new Vector3(10, 0, 20);
            Quaternion rotation = Quaternion.Euler(0, 45, 0);
            Vector3 scale = new Vector3(1.5f, 1.5f, 1.5f);
            PartialWorldStateEntity msg = new PartialWorldStateEntity
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
            PartialWorldStateEntity msg = new PartialWorldStateEntity
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

            GameObject go = new GameObject();
            _createdObjects.Add(go);

            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();

            PartialWorldStateEntity msg = new PartialWorldStateEntity
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

            // set to opposite to make sure it is changed
            identity.hasAuthority = !isOwner;

            NetworkClient.ApplySpawnPayload(identity, msg);

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

            PartialWorldStateEntity msg = new PartialWorldStateEntity
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

            NetworkClient.ApplySpawnPayload(identity, msg);

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
            PartialWorldStateEntity msg = new PartialWorldStateEntity
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

            NetworkClient.ApplySpawnPayload(identity, msg);

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

            PartialWorldStateEntity msg = new PartialWorldStateEntity
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

            NetworkClient.ApplySpawnPayload(identity, msg);

            Assert.That(identity.assetId, Is.EqualTo(guid), "AssetId should not have changed");
        }

        [Test]
        public void ApplyPayload_SendsDataToNetworkBehaviourDeserialize()
        {
            const int value = 12;
            Vector3 direction = new Vector3(0, 1, 1);

            const uint netId = 1000;

            // server object
            GameObject serverObject = new GameObject();
            _createdObjects.Add(serverObject);
            NetworkIdentity serverIdentity = serverObject.AddComponent<NetworkIdentity>();
            PayloadTestBehaviour serverPayloadBehaviour = serverObject.AddComponent<PayloadTestBehaviour>();

            // client object
            GameObject clientObject = new GameObject();
            _createdObjects.Add(clientObject);
            NetworkIdentity clientIdentity = clientObject.AddComponent<NetworkIdentity>();
            PayloadTestBehaviour clientPayloadBehaviour = clientObject.AddComponent<PayloadTestBehaviour>();

            int onSerializeCalled = 0;
            serverPayloadBehaviour.OnSerializeCalled += () => { onSerializeCalled++; };

            int onDeserializeCalled = 0;
            clientPayloadBehaviour.OnDeserializeCalled += () => { onDeserializeCalled++; };

            serverPayloadBehaviour.value = value;
            serverPayloadBehaviour.direction = direction;

            NetworkWriter ownerWriter = new NetworkWriter();
            NetworkWriter observersWriter = new NetworkWriter();
            serverIdentity.OnSerializeAllSafely(true, ownerWriter, out int ownerWritten, observersWriter, out int observersWritten);

            // check that Serialize was called
            Assert.That(onSerializeCalled, Is.EqualTo(1));

            // create spawn message
            PartialWorldStateEntity msg = new PartialWorldStateEntity
            {
                netId = netId,
                payload = ownerWriter.ToArraySegment(),
            };

            // check values start default
            Assert.That(onDeserializeCalled, Is.EqualTo(0));
            Assert.That(clientPayloadBehaviour.value, Is.EqualTo(0));
            Assert.That(clientPayloadBehaviour.direction, Is.EqualTo(Vector3.zero));

            NetworkClient.ApplySpawnPayload(clientIdentity, msg);

            // check values have been set by payload
            Assert.That(onDeserializeCalled, Is.EqualTo(1));
            Assert.That(clientPayloadBehaviour.value, Is.EqualTo(value));
            Assert.That(clientPayloadBehaviour.direction, Is.EqualTo(direction));
        }

        [Test]
        public void ApplyPayload_LocalPlayerAddsIdentityToConnection()
        {
            Debug.Assert(NetworkClient.localPlayer == null, "LocalPlayer should be null before this test");
            const uint netId = 1000;

            GameObject go = new GameObject();
            _createdObjects.Add(go);

            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();

            PartialWorldStateEntity msg = new PartialWorldStateEntity
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

            NetworkClient.connection = new FakeNetworkConnection();
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

            GameObject go = new GameObject();
            _createdObjects.Add(go);

            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();

            PartialWorldStateEntity msg = new PartialWorldStateEntity
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
            NetworkClient.ApplySpawnPayload(identity, msg);

            Assert.That(NetworkClient.localPlayer, Is.EqualTo(identity));
        }
    }
}
