using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class NetworkConnectionToClientTests
    {
        GameObject transportGO;

        [SetUp]
        public void SetUp()
        {
            // transport is needed by server and client.
            // it needs to be on a gameobject because client.connect enables it,
            // which throws a NRE if not on a gameobject
            transportGO = new GameObject();
            Transport.activeTransport = transportGO.AddComponent<MemoryTransport>();
        }

        [TearDown]
        public void TearDown()
        {
            GameObject.DestroyImmediate(transportGO);
        }

        [Test]
        public void Send_BatchInterval_0()
        {
            NetworkConnectionToClient connection = new NetworkConnectionToClient(42, 0);
        }
    }
}
