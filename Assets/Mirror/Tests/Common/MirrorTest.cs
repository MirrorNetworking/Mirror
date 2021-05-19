// base class for networking tests to make things easier.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public abstract class MirrorTest
    {
        // keep track of networked GameObjects so we don't have to clean them
        // up manually each time.
        // CreateNetworked() adds to the list automatically.
        public List<GameObject> instantiated;

        [SetUp]
        public virtual void SetUp()
        {
            instantiated = new List<GameObject>();
        }

        [TearDown]
        public virtual void TearDown()
        {
            foreach (GameObject go in instantiated)
                GameObject.DestroyImmediate(go);
        }

        // create GameObject + NetworkIdentity
        // add to tracker list if needed (useful for cleanups afterwards)
        public void CreateNetworked(out GameObject go, out NetworkIdentity identity)
        {
            go = new GameObject();
            identity = go.AddComponent<NetworkIdentity>();
            instantiated.Add(go);
        }

        // create GameObject + NetworkIdentity + NetworkBehaviour<T>
        // add to tracker list if needed (useful for cleanups afterwards)
        public void CreateNetworked<T>(out GameObject go, out NetworkIdentity identity, out T component)
            where T : NetworkBehaviour
        {
            go = new GameObject();
            identity = go.AddComponent<NetworkIdentity>();
            component = go.AddComponent<T>();
            instantiated.Add(go);
        }
    }
}
