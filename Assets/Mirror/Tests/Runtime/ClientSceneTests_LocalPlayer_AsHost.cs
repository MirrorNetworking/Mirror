using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime
{
    public class ClientSceneTest_LocalPlayer_AsHost : MirrorPlayModeTest
    {
        [UnitySetUp]
        public override IEnumerator UnitySetUp()
        {
            yield return base.UnitySetUp();

            NetworkServer.Listen(1);
            ConnectHostClientBlockingAuthenticatedAndReady();
        }

        [UnityTest]
        public IEnumerator LocalPlayerIsSetToNullAfterNetworkDestroy()
        {
            // need spawned local player
            CreateNetworkedAndSpawnPlayer(out _, out NetworkIdentity identity, NetworkServer.localConnection);

            // need to have localPlayer set for this test
            Assert.That(NetworkClient.localPlayer, !Is.Null);

            // unspawn, wait one frame, localPlayer should be cleared
            NetworkServer.Destroy(identity.gameObject);
            yield return null;
            Assert.IsTrue(NetworkClient.localPlayer is null, "local player should be set to c# null");
        }

        [UnityTest]
        public IEnumerator LocalPlayerIsSetToNullAfterNetworkUnspawn()
        {
            // need spawned local player
            CreateNetworkedAndSpawnPlayer(out _, out NetworkIdentity identity, NetworkServer.localConnection);

            // need to have localPlayer set for this test
            Assert.That(NetworkClient.localPlayer, !Is.Null);

            // unspawn, wait one frame, localPlayer should be cleared
            NetworkServer.UnSpawn(identity.gameObject);
            yield return null;
            Assert.IsTrue(NetworkClient.localPlayer is null, "local player should be set to c# null");
        }
    }
}
