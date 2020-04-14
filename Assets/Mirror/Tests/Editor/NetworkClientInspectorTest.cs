using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    [TestFixture(Category = "NetworkClient")]
    public class NetworkClientInspectorTest
    {

        [Test]
        public void RegisterPrefabs()
        {
            var gameObject = new GameObject("NetworkClient", typeof(NetworkClient));

            NetworkClient client = gameObject.GetComponent<NetworkClient>();

            NetworkClientInspector inspector = ScriptableObject.CreateInstance<NetworkClientInspector>();
            inspector.RegisterPrefabs(client);

            Assert.That(client.spawnPrefabs, Has.Count.GreaterThan(13));

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

            var gameObject = new GameObject("NetworkClient", typeof(NetworkClient));
            NetworkClient client = gameObject.GetComponent<NetworkClient>();
            client.spawnPrefabs.Add(preexisting);

            NetworkClientInspector inspector = ScriptableObject.CreateInstance<NetworkClientInspector>();

            inspector.RegisterPrefabs(client);

            Assert.That(client.spawnPrefabs, Contains.Item(preexisting));

            GameObject.DestroyImmediate(gameObject);
            GameObject.DestroyImmediate(preexisting);
        }
    }
}
