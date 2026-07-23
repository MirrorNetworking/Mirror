using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.SyncVars
{
    class ClientSceneReobserveHookBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnValueChanged))]
        public int value = 42;

        public readonly List<(int oldValue, int newValue)> hookValues = new List<(int oldValue, int newValue)>();

        void OnValueChanged(int oldValue, int newValue) => hookValues.Add((oldValue, newValue));
    }

    public class SyncVarAttributeHook_ClientSceneReobserveTest : MirrorEditModeTest
    {
        NetworkConnectionToClient connectionToClient;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out connectionToClient);
        }

        [Test]
        public void SceneObject_ReplaysHookFromOriginalBaselineWhenObservedAgain()
        {
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity, out ClientSceneReobserveHookBehaviour serverBehaviour,
                out GameObject clientGO, out _, out ClientSceneReobserveHookBehaviour clientBehaviour);

            serverBehaviour.value = 100;
            ProcessMessages();
            Assert.That(clientBehaviour.hookValues, Is.EqualTo(new[] { (42, 100) }));

            NetworkServer.HideForConnection(serverIdentity, connectionToClient);
            ProcessMessages();

            Assert.That(clientGO.activeSelf, Is.False);
            Assert.That(NetworkClient.spawned.ContainsKey(serverIdentity.netId), Is.False);

            NetworkServer.ShowForConnection(serverIdentity, connectionToClient);
            ProcessMessages();

            Assert.That(clientGO.activeSelf, Is.True);
            Assert.That(NetworkClient.spawned.ContainsKey(serverIdentity.netId), Is.True);
            Assert.That(clientBehaviour.hookValues, Is.EqualTo(new[] { (42, 100), (42, 100) }));
        }
    }
}
