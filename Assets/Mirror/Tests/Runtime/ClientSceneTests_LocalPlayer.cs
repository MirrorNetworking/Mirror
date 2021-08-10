using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime.ClientSceneTests
{
    public class ClientSceneTests_LocalPlayer : ClientSceneTestsBase
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            NetworkServer.Listen(1);
            ConnectHostClientBlockingAuthenticatedAndReady();
        }

        [UnityTest]
        public IEnumerator LocalPlayerIsSetToNullAfterDestroy()
        {
            // need spawned local player
            CreateNetworkedAndSpawnPlayer(out GameObject go, out NetworkIdentity identity, NetworkServer.localConnection);

            // need to have localPlayer set for this test
            Assert.That(NetworkClient.localPlayer, !Is.Null);

            GameObject.Destroy(go);

            // wait a frame for destroy to happen
            yield return null;

            // use "is null" here to avoid unity == check
            Assert.IsTrue(NetworkClient.localPlayer is null, "local player should be set to c# null");
        }

        [UnityTest]
        public IEnumerator DestroyingOtherObjectDoesntEffectLocalPlayer()
        {
            // need spawned not-local-player
            CreateNetworkedAndSpawn(out _, out NetworkIdentity notPlayer);

            // need spawned local player
            CreateNetworkedAndSpawnPlayer(out _, out NetworkIdentity player, NetworkServer.localConnection);

            // need to have localPlayer set for this test
            Assert.That(NetworkClient.localPlayer, !Is.Null);

            GameObject.Destroy(notPlayer);

            // wait a frame for destroy to happen
            yield return null;

            Assert.IsTrue(NetworkClient.localPlayer != null, "local player should not be cleared");
            Assert.IsTrue(NetworkClient.localPlayer == player, "local player should still be equal to player");
        }

        [UnityTest]
        public IEnumerator LocalPlayerIsSetToNullAfterDestroyMessage()
        {
            // need spawned local player
            CreateNetworkedAndSpawnPlayer(out GameObject go, out NetworkIdentity identity, NetworkServer.localConnection);

            // need to have localPlayer set for this test
            Assert.That(NetworkClient.localPlayer, !Is.Null);

            NetworkClient.OnObjectDestroy(new ObjectDestroyMessage
            {
                netId = identity.netId
            });

            // wait a frame for destroy to happen
            yield return null;

            // use "is null" here to avoid unity == check
            Assert.IsTrue(NetworkClient.localPlayer is null, "local player should be set to c# null");
        }
    }
}
