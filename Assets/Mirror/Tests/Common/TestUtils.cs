// some helper functions to make testing easier

using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Tests
{
    public static class TestUtils
    {
        // create GameObject + NetworkIdentity + T
        // add to tracker list if needed (useful for cleanups afterwards)
        public static T CreateBehaviour<T>(List<GameObject> tracker = null)
            where T : NetworkBehaviour
        {
            GameObject go = new GameObject();
            go.AddComponent<NetworkIdentity>();
            tracker?.Add(go);
            return go.AddComponent<T>();
        }
    }
}
