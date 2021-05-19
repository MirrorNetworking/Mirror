// some helper functions to make testing easier

using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Tests
{
    public static class TestUtils
    {
        // create GameObject + NetworkIdentity
        // add to tracker list if needed (useful for cleanups afterwards)
        public static void CreateNetworked(out GameObject go, out NetworkIdentity identity, List<GameObject> tracker = null)
        {
            go = new GameObject();
            identity = go.AddComponent<NetworkIdentity>();
            tracker?.Add(go);
        }

        // create GameObject + NetworkIdentity + NetworkBehaviour<T>
        // add to tracker list if needed (useful for cleanups afterwards)
        public static void CreateNetworked<T>(out GameObject go, out NetworkIdentity identity, out T component, List<GameObject> tracker = null)
            where T : NetworkBehaviour
        {
            go = new GameObject();
            identity = go.AddComponent<NetworkIdentity>();
            component = go.AddComponent<T>();
            tracker?.Add(go);
        }
    }
}
