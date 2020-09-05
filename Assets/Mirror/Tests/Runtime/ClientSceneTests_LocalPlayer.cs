using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime.ClientSceneTests
{
    public class ClientSceneTests_LocalPlayer : ClientSceneTestsBase
    {
        NetworkIdentity SpawnLocalPlayer()
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
            };

            PropertyInfo readyConnProperty = typeof(ClientScene).GetProperty(nameof(ClientScene.readyConnection));
            readyConnProperty.SetValue(null, new FakeNetworkConnection());

            ClientScene.ApplySpawnPayload(identity, msg);

            Assert.That(ClientScene.localPlayer, Is.EqualTo(identity));

            return identity;
        }

        [UnityTest]
        public IEnumerator LocalPlayer_IsSetToNullAfterDestroy()
        {
            NetworkIdentity identity = SpawnLocalPlayer();

            GameObject.Destroy(identity);

            // wait a frame for destroy to happen
            yield return null;

            // use "is null" here to avoid unity == check
            Assert.IsTrue(ClientScene.localPlayer is null, "local player should be set to c# null");
        }
    }
}
