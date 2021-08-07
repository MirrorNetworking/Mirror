using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime
{
    public class ClientSceneTest_LocalPlayer_AsHost : HostSetup
    {
        [UnityTest]
        public IEnumerator LocalPlayerIsSetToNullAfterNetworkDestroy()
        {
            const uint netId = 1000;
            CreateNetworked(out GameObject go, out NetworkIdentity identity);

            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                isLocalPlayer = true,
                isOwner = true,
            };


            NetworkIdentity.spawned[msg.netId] = identity;
            NetworkClient.OnHostClientSpawn(msg);

            NetworkServer.Destroy(identity.gameObject);

            // wait a frame for destroy to happen
            yield return null;

            // use "is null" here to avoid unity == check
            Assert.IsTrue(NetworkClient.localPlayer is null, "local player should be set to c# null");
        }

        [UnityTest]
        public IEnumerator LocalPlayerIsSetToNullAfterNetworkUnspawn()
        {
            const uint netId = 1000;
            CreateNetworked(out GameObject go, out NetworkIdentity identity);

            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                isLocalPlayer = true,
                isOwner = true,
            };

            NetworkIdentity.spawned[msg.netId] = identity;
            NetworkClient.OnHostClientSpawn(msg);

            NetworkServer.UnSpawn(identity.gameObject);

            // wait a frame for destroy to happen
            yield return null;

            // use "is null" here to avoid unity == check
            Assert.IsTrue(NetworkClient.localPlayer is null, "local player should be set to c# null");
        }
    }
}
