using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    [TestFixture(Category = "ClientObjectManager")]
    public class ClientObjectManagerInspectorTest
    {

        [Test]
        public void RegisterPrefabs()
        {
            var gameObject = new GameObject("NetworkObjectManager", typeof(ClientObjectManager));

            ClientObjectManager client = gameObject.GetComponent<ClientObjectManager>();

            ClientObjectManagerInspector inspector = ScriptableObject.CreateInstance<ClientObjectManagerInspector>();
            inspector.RegisterPrefabs(client);

            Assert.That(client.spawnPrefabs, Has.Count.GreaterThan(2));

            foreach (var prefab in client.spawnPrefabs)
            {
                Assert.That(prefab.GetComponent<NetworkIdentity>(), Is.Not.Null);
            }
            GameObject.DestroyImmediate(gameObject);
        }

        [Test]
        public void PreserveExisting()
        {
            var preexisting = new GameObject("object", typeof(NetworkIdentity));

            var gameObject = new GameObject("NetworkObjectManager", typeof(ClientObjectManager));
            ClientObjectManager client = gameObject.GetComponent<ClientObjectManager>();
            client.spawnPrefabs.Add(preexisting);

            ClientObjectManagerInspector inspector = ScriptableObject.CreateInstance<ClientObjectManagerInspector>();

            inspector.RegisterPrefabs(client);

            Assert.That(client.spawnPrefabs, Contains.Item(preexisting));

            GameObject.DestroyImmediate(gameObject);
            GameObject.DestroyImmediate(preexisting);
        }
    }
}
