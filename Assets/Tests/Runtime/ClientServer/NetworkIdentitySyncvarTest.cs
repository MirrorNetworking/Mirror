using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Mirror.Tests.ClientServer
{
    public class SampleBehaviorWithNI : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnTargetChange))]
        public NetworkIdentity target;

        public void OnTargetChange(NetworkIdentity _, NetworkIdentity networkIdentity)
        {
            Assert.That(networkIdentity, Is.SameAs(target));
        }
    }

    public class NetworkIdentitySyncvarTest : ClientServerSetup<SampleBehaviorWithNI>
    {
        [Test]
        public void IsNullByDefault()
        {
            // out of the box, target should be null in the client

            Assert.That(clientComponent.target, Is.Null);
        }

        [UnityTest]
        public IEnumerator ChangeTarget() => UniTask.ToCoroutine(async () =>
        {
            serverComponent.target = serverIdentity;

            await UniTask.WaitUntil(() => clientComponent.target != null);

            Assert.That(clientComponent.target, Is.SameAs(clientIdentity));
        });

        [Test]
        public void UpdateAfterSpawn()
        {
            // this situation can happen when the client does nto see an object
            // but the object is assigned in a syncvar.
            // this can easily happen during spawn if spawning in an unexpected order
            // or if there is AOI in play.
            // in this case we would have a valid net id, but we would not
            // find the object at spawn time

            var networkIdentitySyncvar = new NetworkIdentitySyncvar
            {
                client = client,
                netId = serverIdentity.NetId,
                identity = null,
            };

            Assert.That(networkIdentitySyncvar.Value, Is.SameAs(clientIdentity));
        }

        [UnityTest]
        public IEnumerator SpawnWithTarget() => UniTask.ToCoroutine(async () =>
        {
            // create an object, set the target and spawn it
            UnityEngine.GameObject newObject = UnityEngine.Object.Instantiate(playerPrefab);
            SampleBehaviorWithNI newBehavior = newObject.GetComponent<SampleBehaviorWithNI>();
            newBehavior.target = serverIdentity;
            serverObjectManager.Spawn(newObject);

            // wait until the client spawns it
            uint newObjectId = newBehavior.NetId;
            await UniTask.WaitUntil(() => client.Spawned.ContainsKey(newObjectId));

            // check if the target was set correctly in the client
            NetworkIdentity newClientObject = client.Spawned[newObjectId];
            SampleBehaviorWithNI newClientBehavior = newClientObject.GetComponent<SampleBehaviorWithNI>();
            Assert.That(newClientBehavior.target, Is.SameAs(clientIdentity));

            // cleanup
            serverObjectManager.Destroy(newObject);
        });
    }
}
